using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZF.EraGallery;
using ZF.EraGallery.EditorTools;
using Object = UnityEngine.Object;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 一键搭出「场景解密」的演示：
    ///   石器时代窗口里放一块**岩壁**和一堆**柴堆**，信息时代窗口里放一个**壁炉**。
    ///   演示的谜题链：岩壁抠出燧石 → 用燧石点着柴堆（fire_lit = 1）→ **信息时代的壁炉自己烧起来**。
    ///   最后一步就是"原始人钻木取火，现代人的壁炉跟着点着"那个联动，规则表里一个字都没提跨时代。
    ///
    /// 菜单：Tools → 谜题 → 搭建解密演示
    /// </summary>
    public static class PuzzleDemoBuilder
    {
        private const string GeneratedFolder = "Assets/Scripts/Puzzle/Generated";
        private const string TablePath = GeneratedFolder + "/PuzzleTable_Demo.asset";
        private const string RootName = "PuzzleRoot";
        private const string EraRootName = "EraWorld";

        private const string InteractablePrefix = "Interactable_";
        private const string CharacterPrefix = "Character_";
        private const string HaloName = "HoverHalo";

        /// <summary>时间裂隙的物体 id。规则表里按 id 引用它。</summary>
        private static string RiftId(EraId era) => "rift_" + era.ToString().ToLowerInvariant();

        // 层级：内容 0 / 地景 5 / 时代序号点 7 / 边框 10 / 锁-对勾 20（EraWindowSceneBuilder 定的）
        private const int HaloOrder = PuzzleSortingOrder.Halo;

        /// <summary>
        /// ★ 同一个东西里**重叠的形状必须给递增的层级**（layer 参数）。
        /// 层级相同又重叠时画的顺序是不确定的 —— 踩过的坑：壁炉本体和火苗同层级，
        /// 本体有时候盖在火苗上，表现成"石器时代点了火、壁炉却没火；有时候又有火；有时候只有下面一个火苗"。
        /// </summary>
        private static int ShapeOrder(int layer) => PuzzleSortingOrder.ObjectBase + layer;

        private static readonly Color RockBody = new Color(0.46f, 0.45f, 0.49f, 1f);
        private static readonly Color RockLight = new Color(0.58f, 0.57f, 0.62f, 1f);
        private static readonly Color RockDark = new Color(0.35f, 0.34f, 0.38f, 1f);
        private static readonly Color LogBody = new Color(0.42f, 0.29f, 0.18f, 1f);
        private static readonly Color LogDark = new Color(0.32f, 0.22f, 0.14f, 1f);
        private static readonly Color FlameLow = new Color(0.95f, 0.55f, 0.16f, 1f);
        private static readonly Color FlameHigh = new Color(1f, 0.82f, 0.36f, 1f);
        private static readonly Color HearthBody = new Color(0.30f, 0.30f, 0.36f, 1f);
        private static readonly Color HearthHole = new Color(0.09f, 0.09f, 0.12f, 1f);
        private static readonly Color RiftDim = new Color(0.45f, 0.62f, 0.72f, 0.30f);
        private static readonly Color RiftSoft = new Color(0.55f, 0.95f, 1f, 0.22f);
        private static readonly Color RiftBright = new Color(0.72f, 0.98f, 1f, 1f);

        // 人物。石器时代故意放**两个** —— 不然"只送一个人"和"整个时代一起走"看起来一模一样，分不出区别。
        private static readonly string[] CharacterIds = { "ayan", "ashi", "tong", "deng", "ling" };

        private static readonly string[] CharacterNames = { "阿岩", "阿石", "老铜", "小灯", "零" };

        private static readonly EraId[] CharacterHomes =
        {
            EraId.Stone,        // 阿岩
            EraId.Stone,        // 阿石
            EraId.Steam,        // 老铜
            EraId.Electric,     // 小灯
            EraId.Information,  // 零
        };

        private static readonly string[] CharacterLines =
        {
            "阿岩：「火得有人守着。别的年头，我也想去看看。」",
            "阿石：「我跟你一块儿去，路上有个照应。」",
            "老铜：「齿轮转起来了，就差一口热气。」",
            "小灯：「灯是亮的，可线是断的。」",
            "零：「信号里有人，一直在重复同一句话。」",
        };

        private static readonly Color[] CharacterColors =
        {
            new Color(0.80f, 0.50f, 0.30f, 1f),
            new Color(0.62f, 0.44f, 0.34f, 1f),
            new Color(0.78f, 0.62f, 0.24f, 1f),
            new Color(0.32f, 0.68f, 0.84f, 1f),
            new Color(0.62f, 0.50f, 0.88f, 1f),
        };

        // ===================== 菜单 =====================

        [MenuItem("Tools/谜题/搭建解密演示", false, 20)]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[谜题] Play 模式下不能改场景，先停下来再点这个菜单。");
                return;
            }

            if (GameObject.Find(EraRootName) == null)
            {
                Debug.LogError("[谜题] 场景里没有 EraWorld。先跑 Tools/时代窗口/搭建四个时代窗口场景。");
                return;
            }

            // 每个时代都要有人物和一个时间裂隙，所以四个窗口都得在
            List<EraWindow> windows = new List<EraWindow>();
            for (int i = 0; i < EraCatalog.All.Length; i++)
            {
                EraWindow window = FindWindow(EraCatalog.All[i].id);
                if (window == null)
                {
                    Debug.LogError($"[谜题] 找不到「{EraCatalog.All[i].title}」的窗口。先跑一次 Tools/时代窗口/搭建四个时代窗口场景。");
                    return;
                }

                windows.Add(window);
            }

            Sprite white = EraWindowSceneBuilder.EnsureWhiteSprite();
            if (white == null)
            {
                Debug.LogError("[谜题] 占位方块图拿不到，搭不下去。");
                return;
            }

            ClearDemoObjects();

            // 规则表：老版本的表里没有人物/裂隙那些规则（或没有"点名"），场景搭好了也点不动 —— 检测到就直接重建。
            // （平时表是新的就不动它，免得把你在 Inspector 里改过的规则冲掉）
            PuzzleTableSO existing = AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(TablePath);
            bool stale = existing != null &&
                         (!HasRuleFor(existing, RiftId(EraId.Stone)) || !HasEffect<SelectCharacterEffect>(existing));

            PuzzleTableSO table = EnsureTable(stale);
            if (stale)
            {
                Debug.Log("[谜题] 演示规则表是旧版本的（没有人物/时间裂隙/点名的规则），已按新演示内容重建 —— " +
                          "你在 Inspector 里改过的演示规则被覆盖了。");
            }

            // 物件（windows[i] 的 i 就是 (int)EraId，和 EraWorldController 排序后的窗口序号是同一个约定）
            BuildRock(windows[(int)EraId.Stone].transform, white);
            BuildWoodpile(windows[(int)EraId.Stone].transform, white);
            BuildHearth(windows[(int)EraId.Information].transform, white);

            // 每个时代一个时间裂隙
            for (int i = 0; i < windows.Count; i++)
            {
                BuildRift(windows[i].transform, EraCatalog.All[i].id, white);
            }

            // 人物：按各自的 homeEra 挂到对应窗口下面。同代多个人在编辑器里也按运行时那套公式错开。
            int[] eraCounts = new int[EraCatalog.All.Length];
            int[] eraCursor = new int[EraCatalog.All.Length];
            for (int i = 0; i < CharacterIds.Length; i++)
            {
                eraCounts[(int)CharacterHomes[i]]++;
            }

            for (int i = 0; i < CharacterIds.Length; i++)
            {
                int eraIndex = (int)CharacterHomes[i];
                int slot = eraCursor[eraIndex]++;
                float x = (slot - (eraCounts[eraIndex] - 1) * 0.5f) * 0.70f;

                BuildCharacter(windows[eraIndex].transform, i, new Vector3(x, -0.70f, 0f), white);
            }

            BuildRoot(table);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[谜题] 解密演示搭好了。石器时代有**两个人**（阿岩、阿石），其余每个时代一个人；每个时代还有一个**时间裂隙**。\n" +
                      "  按 Play，然后：\n" +
                      "  1. 进石器时代 → 点岩壁拿「燧石」→ Tab 拿在手里 → 点柴堆点火\n" +
                      "     （火一起，四个时代的裂隙同时张开；石器时代窗口亮对勾）\n" +
                      "  2. 【单独送一个人】点**阿岩**（他被点名，身上会亮框）→ 点**时间裂隙** → 只有他一个人过去，阿石留下\n" +
                      "  3. 【整个时代一起走】点阿岩再点一次取消点名 → 点**时间裂隙** → 石器时代的人一起过去\n" +
                      "  4. 进蒸汽时代看：阿岩/阿石站在老铜旁边，三个人自动分开站好\n" +
                      "     （「蒸汽时代里有两个人」这个谜题这时会自己完成 → 演示「一起解密」的判定）\n" +
                      "  5. 回全景进信息时代 → **壁炉自己烧起来了**（石器时代点的火）\n" +
                      "  点人物 = 点名/取消点名（也会说一句台词）。规则表在 " + TablePath + "，改规则不用改代码。");
        }

        [MenuItem("Tools/谜题/清掉解密演示", false, 21)]
        public static void Clear()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[谜题] Play 模式下不能改场景。");
                return;
            }

            ClearDemoObjects();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[谜题] 已清掉解密演示（规则表资产留着）。");
        }

        [MenuItem("Tools/谜题/重建演示规则表（覆盖）", false, 22)]
        public static void RebuildTable()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[谜题] Play 模式下不能改资产。");
                return;
            }

            PuzzleTableSO table = EnsureTable(true);
            Selection.activeObject = table;
            Debug.Log($"[谜题] 演示规则表已重建：{TablePath}");
        }

        // ===================== 演示物 =====================

        private static void BuildRock(Transform window, Sprite white)
        {
            Interactable target = CreateInteractable(window, "rock", EraId.Stone, "岩壁");
            List<SpriteRenderer> shapes = new List<SpriteRenderer>
            {
                Create(target.transform, "RockA", new Vector2(-2.35f, 0.30f), new Vector2(0.88f, 0.5f), RockBody, white, 0),
                Create(target.transform, "RockB", new Vector2(-2.52f, 0.70f), new Vector2(0.5f, 0.44f), RockLight, white, 1),
                Create(target.transform, "RockC", new Vector2(-2.14f, 0.66f), new Vector2(0.42f, 0.5f), RockDark, white, 2),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = PuzzleStates.Default, objects = ToObjects(shapes) },
            };

            Finish(target, "rock", EraId.Stone, "岩壁", PuzzleStates.Default, groups, null, window, shapes, white);
        }

        private static void BuildWoodpile(Transform window, Sprite white)
        {
            Interactable target = CreateInteractable(window, "woodpile", EraId.Stone, "柴堆");

            // 两个状态组各留一份柴：状态组是互斥的，切到"着火了"就把"没着火"那一组整个关掉
            List<SpriteRenderer> cold = new List<SpriteRenderer>
            {
                Create(target.transform, "LogA", new Vector2(1.90f, -1.15f), new Vector2(1.30f, 0.22f), LogBody, white, 0),
                Create(target.transform, "LogB", new Vector2(1.90f, -0.93f), new Vector2(1.16f, 0.22f), LogDark, white, 1),
                Create(target.transform, "LogC", new Vector2(1.90f, -0.71f), new Vector2(0.96f, 0.22f), LogBody, white, 2),
            };

            List<SpriteRenderer> lit = new List<SpriteRenderer>
            {
                Create(target.transform, "LitLogA", new Vector2(1.90f, -1.15f), new Vector2(1.30f, 0.22f), LogBody, white, 0),
                Create(target.transform, "LitLogB", new Vector2(1.90f, -0.93f), new Vector2(1.16f, 0.22f), LogDark, white, 1),
                Create(target.transform, "LitLogC", new Vector2(1.90f, -0.71f), new Vector2(0.96f, 0.22f), LogBody, white, 2),
                Create(target.transform, "FlameLow", new Vector2(1.90f, -0.42f), new Vector2(0.50f, 0.44f), FlameLow, white, 3),
                Create(target.transform, "FlameHigh", new Vector2(1.90f, -0.13f), new Vector2(0.28f, 0.36f), FlameHigh, white, 4),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = PuzzleStates.Default, objects = ToObjects(cold) },
                new StateGroup { state = "lit", objects = ToObjects(lit) },
            };

            SetGroupActive(groups[1], false);

            List<SpriteRenderer> all = new List<SpriteRenderer>(cold);
            all.AddRange(lit);

            Finish(target, "woodpile", EraId.Stone, "柴堆", PuzzleStates.Default, groups, null, window, all, white);
        }

        private static void BuildHearth(Transform window, Sprite white)
        {
            Interactable target = CreateInteractable(window, "hearth", EraId.Information, "壁炉");

            List<SpriteRenderer> cold = new List<SpriteRenderer>
            {
                Create(target.transform, "HearthBody", new Vector2(1.85f, -0.95f), new Vector2(1.60f, 1.20f), HearthBody, white, 0),
                Create(target.transform, "HearthHole", new Vector2(1.85f, -1.00f), new Vector2(0.90f, 0.70f), HearthHole, white, 1),
            };

            // 本体 0 < 炉膛 1 < 下火苗 2 < 上火苗 3 —— 递增，重叠也不会画得不确定
            List<SpriteRenderer> lit = new List<SpriteRenderer>
            {
                Create(target.transform, "LitHearthBody", new Vector2(1.85f, -0.95f), new Vector2(1.60f, 1.20f), HearthBody, white, 0),
                Create(target.transform, "LitHearthHole", new Vector2(1.85f, -1.00f), new Vector2(0.90f, 0.70f), HearthHole, white, 1),
                Create(target.transform, "LitFlameLow", new Vector2(1.85f, -0.86f), new Vector2(0.56f, 0.60f), FlameLow, white, 2),
                Create(target.transform, "LitFlameHigh", new Vector2(1.85f, -0.58f), new Vector2(0.32f, 0.44f), FlameHigh, white, 3),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = "cold", objects = ToObjects(cold) },
                new StateGroup { state = "lit", objects = ToObjects(lit) },
            };

            SetGroupActive(groups[1], false);

            // 跨时代表现联动就这一条：石器时代把 fire_lit 点着了，这个壁炉自己就切到 lit
            List<VisualStateRule> rules = new List<VisualStateRule>
            {
                new VisualStateRule
                {
                    note = "石器时代点了火，壁炉跟着烧起来",
                    state = "lit",
                    conditions = Conditions(FlagOn("fire_lit")),
                },
            };

            List<SpriteRenderer> all = new List<SpriteRenderer>(cold);
            all.AddRange(lit);

            Finish(target, "hearth", EraId.Information, "壁炉", "cold", groups, rules, window, all, white);
        }

        /// <summary>
        /// 一个人物。这里的 localPosition 只是"编辑器里先摆在这儿"，运行时 CharacterView 会按
        /// 「这个时代现在有几个人」自动重新排站位（所以搬走/搬来之后剩下的人会自己站好）。
        /// </summary>
        private static void BuildCharacter(Transform window, int index, Vector3 previewPosition, Sprite white)
        {
            string id = CharacterIds[index];
            Color body = CharacterColors[index];
            Color head = Color.Lerp(body, Color.white, 0.35f);

            GameObject go = new GameObject(CharacterPrefix + id);
            go.transform.SetParent(window, false);
            go.transform.localPosition = previewPosition;

            CharacterView view = go.AddComponent<CharacterView>();
            BoxCollider2D hitArea = go.AddComponent<BoxCollider2D>();
            hitArea.isTrigger = true;

            // 火柴人：两条腿 + 身子 + 脑袋。腿 0/1、身子 2、脑袋 3 —— 递增层级，重叠也不会画得不确定
            List<SpriteRenderer> shapes = new List<SpriteRenderer>
            {
                CreateCharacterShape(go.transform, "LegA", new Vector2(-0.085f, -0.44f), new Vector2(0.11f, 0.36f), body, white, 0),
                CreateCharacterShape(go.transform, "LegB", new Vector2(0.085f, -0.44f), new Vector2(0.11f, 0.36f), body, white, 1),
                CreateCharacterShape(go.transform, "Body", new Vector2(0f, -0.04f), new Vector2(0.38f, 0.48f), body, white, 2),
                CreateCharacterShape(go.transform, "Head", new Vector2(0f, 0.36f), new Vector2(0.30f, 0.30f), head, white, 3),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = PuzzleStates.Default, objects = ToObjects(shapes) },
            };

            // 人物会在时代之间搬家，所以高亮框必须挂在**人物自己**身上（挂在窗口上就不会跟着走了）
            SpriteRenderer halo = CreateHalo(go.transform, shapes, white);

            SerializedObject so = new SerializedObject(view);
            SetString(so, "characterId", id);
            SetString(so, "displayName", CharacterNames[index]);
            SetInt(so, "homeEra", (int)CharacterHomes[index]);
            SetRef(so, "hitArea", hitArea);
            SetRef(so, "hoverFrame", halo);
            WriteStateGroups(so, groups);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 一个时代里的时间裂隙。做成一开一闭两个状态：
        /// 这个时代通关了（演示里用全局 flag fire_lit 当门槛）它就张开，点它把这个时代的人整体送到下一个时代。
        /// </summary>
        private static void BuildRift(Transform window, EraId era, Sprite white)
        {
            string id = RiftId(era);
            Interactable target = CreateInteractable(window, id, era, "时间裂隙");

            List<SpriteRenderer> closed = new List<SpriteRenderer>
            {
                Create(target.transform, "RiftDim", new Vector2(2.55f, 1.15f), new Vector2(0.34f, 0.86f), RiftDim, white, 0),
            };

            // 光晕 0 < 裂缝 1/2/3 —— 递增
            List<SpriteRenderer> open = new List<SpriteRenderer>
            {
                Create(target.transform, "RiftGlow", new Vector2(2.55f, 1.15f), new Vector2(0.62f, 1.22f), RiftSoft, white, 0),
                Create(target.transform, "CrackA", new Vector2(2.47f, 1.30f), new Vector2(0.13f, 0.34f), RiftBright, white, 1),
                Create(target.transform, "CrackB", new Vector2(2.63f, 1.04f), new Vector2(0.13f, 0.32f), RiftBright, white, 2),
                Create(target.transform, "CrackC", new Vector2(2.51f, 0.83f), new Vector2(0.13f, 0.26f), RiftBright, white, 3),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = "closed", objects = ToObjects(closed) },
                new StateGroup { state = "open", objects = ToObjects(open) },
            };

            SetGroupActive(groups[1], false);

            // 四个时代的裂隙读的是同一个全局 flag，所以火烧起来时它们会一起张开 ——
            // 这本身就是"状态共享"的又一个例子。正式内容里换成每个时代自己的通关 flag。
            List<VisualStateRule> rules = new List<VisualStateRule>
            {
                new VisualStateRule
                {
                    note = "火种出现了，裂隙就能感觉到",
                    state = "open",
                    conditions = Conditions(FlagOn("fire_lit")),
                },
            };

            List<SpriteRenderer> all = new List<SpriteRenderer>(closed);
            all.AddRange(open);

            Finish(target, id, era, "时间裂隙", "closed", groups, rules, window, all, white);
        }

        // ===================== 规则表 =====================

        /// <summary>表里有没有针对某个目标的规则。用来判断这张表是不是旧版本搭出来的。</summary>
        private static bool HasRuleFor(PuzzleTableSO table, string targetId)
        {
            if (table == null || table.interactionRules == null)
            {
                return false;
            }

            for (int i = 0; i < table.interactionRules.Count; i++)
            {
                InteractionRule rule = table.interactionRules[i];
                if (rule != null && rule.targetId == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>表里有没有用到某种效果。用来判断这张表是不是旧版本搭出来的。</summary>
        private static bool HasEffect<T>(PuzzleTableSO table) where T : PuzzleEffect
        {
            if (table == null || table.interactionRules == null)
            {
                return false;
            }

            for (int i = 0; i < table.interactionRules.Count; i++)
            {
                InteractionRule rule = table.interactionRules[i];
                if (rule?.effects == null)
                {
                    continue;
                }

                for (int j = 0; j < rule.effects.Count; j++)
                {
                    if (rule.effects[j] is T)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>表已经存在就不动它 —— 免得把你在 Inspector 里改过的规则冲掉。</summary>
        private static PuzzleTableSO EnsureTable(bool forceRebuild)
        {
            EraWindowSceneBuilder.EnsureFolder(GeneratedFolder);

            PuzzleTableSO table = AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(TablePath);
            if (table != null && !forceRebuild)
            {
                return table;
            }

            bool isNew = table == null;
            if (isNew)
            {
                table = ScriptableObject.CreateInstance<PuzzleTableSO>();
            }

            table.interactionRules = BuildRules();
            table.puzzles = BuildPuzzles();

            if (isNew)
            {
                AssetDatabase.CreateAsset(table, TablePath);
            }
            else
            {
                EditorUtility.SetDirty(table);
            }

            AssetDatabase.SaveAssets();
            return table;
        }

        private static List<InteractionRule> BuildRules()
        {
            const string Rock = "rock";
            const string Woodpile = "woodpile";
            const string Hearth = "hearth";
            const string Flint = "flint";

            List<InteractionRule> rules = new List<InteractionRule>
            {
                new InteractionRule
                {
                    note = "岩壁：抠出燧石",
                    targetId = Rock,
                    verb = Verb.Interact,
                    conditions = Conditions(Flag("rock_searched", FlagOp.Equals, 0f)),
                    effects = Effects(
                        new GiveItemEffect { itemId = Flint },
                        new SetFlagEffect { flag = "rock_searched", value = 1f },
                        new FeedbackEffect { message = "你从岩缝里抠出一块燧石。" }),
                },
                new InteractionRule
                {
                    note = "岩壁：已经抠过了（兜底，必须排在后面）",
                    targetId = Rock,
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(new FeedbackEffect { message = "岩缝里什么都没有了。" }),
                },
                new InteractionRule
                {
                    note = "柴堆：用燧石打火",
                    targetId = Woodpile,
                    verb = Verb.UseItem,
                    itemId = Flint,
                    conditions = Conditions(Item(Flint)),
                    effects = Effects(
                        new ConsumeItemEffect { itemId = Flint },
                        new SetFlagEffect { flag = "fire_lit", value = 1f },
                        new SetObjectStateEffect { interactableId = Woodpile, state = "lit" },
                        new FeedbackEffect { message = "火星落进干柴，火窜起来了。" }),
                    elseFeedback = "你手里没有能打火的东西。",
                },
                new InteractionRule
                {
                    note = "柴堆：空手点（兜底）",
                    targetId = Woodpile,
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(new FeedbackEffect { message = "一堆干柴。手里得有个火种。" }),
                },
                new InteractionRule
                {
                    note = "壁炉：火种穿过时间（跨时代联动，条件在前）",
                    targetId = Hearth,
                    verb = Verb.Interact,
                    conditions = Conditions(FlagOn("fire_lit")),
                    effects = Effects(
                        new SetFlagEffect { flag = "hearth_lit", value = 1f },
                        new FeedbackEffect { message = "壁炉自己烧起来了 —— 火种穿过了时间。" }),
                },
                new InteractionRule
                {
                    note = "壁炉：还是冷的（兜底，必须排在后面）",
                    targetId = Hearth,
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(new FeedbackEffect { message = "壁炉是冷的，一根柴都没有。" }),
                },
            };

            // ---- 人物：点他 = **点名**（再点一次取消），顺便说一句台词 ----
            // 点名的那个就是"单独送走"的对象，见下面时间裂隙的第 1 条规则。
            for (int i = 0; i < CharacterIds.Length; i++)
            {
                rules.Add(new InteractionRule
                {
                    note = $"人物 {CharacterNames[i]}：已经点着他了 → 再点一次取消（条件在前）",
                    targetId = CharacterIds[i],
                    verb = Verb.Interact,
                    conditions = Conditions(new SelectedCharacterCondition { characterId = CharacterIds[i] }),
                    effects = Effects(
                        new SelectCharacterEffect(),
                        new FeedbackEffect { message = $"取消点名{CharacterNames[i]}。" }),
                });

                rules.Add(new InteractionRule
                {
                    note = $"人物 {CharacterNames[i]}：点名",
                    targetId = CharacterIds[i],
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(
                        new SelectCharacterEffect { characterId = CharacterIds[i] },
                        new FeedbackEffect { message = $"点名{CharacterNames[i]}。{CharacterLines[i]}" }),
                });
            }

            // ---- 时间裂隙：每个时代一个。三条路，条件从特殊到通用 ----
            for (int i = 0; i < EraCatalog.All.Length; i++)
            {
                EraId era = EraCatalog.All[i].id;
                EraId next = EraCatalog.Next(era);
                string eraTitle = EraCatalog.Get(era).title;
                string nextTitle = EraCatalog.Get(next).title;

                // ① 点名了人、而且那个人就在这个时代 → **只送他一个人**
                rules.Add(new InteractionRule
                {
                    note = $"时间裂隙：只送点名的那个人 {eraTitle} → {nextTitle}",
                    targetId = RiftId(era),
                    verb = Verb.Interact,
                    // 条件顺序无所谓（全都要满足），关键是这条规则要排在"全体走"前面
                    conditions = Conditions(FlagOn("fire_lit"), new SelectedCharacterInEraCondition { era = era }),
                    effects = Effects(
                        new MoveSelectedCharacterEffect { targetEra = next },
                        new FeedbackEffect { message = $"你点名的那个人一个人跨进了{nextTitle}。" }),
                });

                // ② 没点名（或点名的人不在这个时代），这个时代有人 → **整个时代一起走**
                rules.Add(new InteractionRule
                {
                    note = $"时间裂隙：整个 {eraTitle} 一起走 → {nextTitle}",
                    targetId = RiftId(era),
                    verb = Verb.Interact,
                    conditions = Conditions(FlagOn("fire_lit"), new CharacterCountInEraCondition
                    {
                        era = era,
                        op = FlagOp.GreaterOrEqual,
                        count = 1f,
                    }),
                    effects = Effects(
                        new MoveEraCharactersEffect { fromEra = era, targetEra = next },
                        new FeedbackEffect { message = $"{eraTitle}的人一起跨进了{nextTitle}的窗口。" }),
                });

                // ③ 裂隙开着但这个时代没人
                rules.Add(new InteractionRule
                {
                    note = "时间裂隙：这个时代没人",
                    targetId = RiftId(era),
                    verb = Verb.Interact,
                    conditions = Conditions(FlagOn("fire_lit")),
                    effects = Effects(new FeedbackEffect { message = $"{eraTitle}里已经没有人了。" }),
                });

                // ④ 兜底：裂隙还闭着（必须排最后）
                rules.Add(new InteractionRule
                {
                    note = "时间裂隙：还闭着（兜底，必须排在后面）",
                    targetId = RiftId(era),
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(new FeedbackEffect { message = "裂隙还闭着。得先让这个年头的火点起来。" }),
                });
            }

            return rules;
        }

        private static List<PuzzleDefinition> BuildPuzzles()
        {
            return new List<PuzzleDefinition>
            {
                new PuzzleDefinition
                {
                    id = "P_get_flint",
                    title = "拿到燧石",
                    era = EraId.Stone,
                    conditions = Conditions(Item("flint")),
                    onSolved = Effects(new FeedbackEffect { message = "（谜题完成）拿到燧石。" }),
                    isMainPuzzle = false,
                },
                new PuzzleDefinition
                {
                    id = "P_stone_fire",
                    title = "钻木取火",
                    era = EraId.Stone,
                    conditions = Conditions(FlagOn("fire_lit")),
                    onSolved = Effects(new FeedbackEffect { message = "（谜题完成）石器时代通关 —— 火种有了。" }),
                    isMainPuzzle = true,   // 主线：解开 = 石器时代通关，窗口亮对勾
                },
                new PuzzleDefinition
                {
                    id = "P_fire_crossed",
                    title = "火种穿过时间",
                    era = EraId.Information,
                    conditions = Conditions(FlagOn("hearth_lit")),
                    onSolved = Effects(new FeedbackEffect { message = "（谜题完成）现代壁炉的火，是石器时代点起来的。" }),
                    isMainPuzzle = false,
                },
                new PuzzleDefinition
                {
                    // 这就是「两个人凑到同一个时代才能一起解密」的判定
                    id = "P_two_together",
                    title = "两个人凑在一起",
                    era = EraId.Steam,
                    conditions = Conditions(new CharacterCountInEraCondition
                    {
                        era = EraId.Steam,
                        op = FlagOp.GreaterOrEqual,
                        count = 2f,
                    }),
                    onSolved = Effects(new FeedbackEffect { message = "（谜题完成）蒸汽时代里凑齐了两个人 —— 可以一起动手了。" }),
                    isMainPuzzle = false,
                },
            };
        }

        private static List<PuzzleCondition> Conditions(params PuzzleCondition[] items) =>
            new List<PuzzleCondition>(items);

        private static List<PuzzleEffect> Effects(params PuzzleEffect[] items) =>
            new List<PuzzleEffect>(items);

        private static FlagCondition Flag(string flag, FlagOp op, float value) =>
            new FlagCondition { flag = flag, op = op, value = value };

        private static FlagCondition FlagOn(string flag) =>
            new FlagCondition { flag = flag, op = FlagOp.Equals, value = 1f };

        private static HasItemCondition Item(string itemId) => new HasItemCondition { itemId = itemId };

        // ===================== 场景搭建 =====================

        private static Interactable CreateInteractable(Transform window, string id, EraId era, string displayName)
        {
            GameObject go = new GameObject(InteractablePrefix + id);
            go.transform.SetParent(window, false);
            go.transform.localPosition = Vector3.zero;   // 形状直接用窗口局部坐标摆，读起来和别的装饰一样

            Interactable interactable = go.AddComponent<Interactable>();

            BoxCollider2D hitArea = go.AddComponent<BoxCollider2D>();
            hitArea.isTrigger = true;   // 只用来点选；Awake 里还会按视觉自动贴合一次

            return interactable;
        }

        private static void Finish(Interactable target, string id, EraId era, string displayName, string defaultState,
            List<StateGroup> groups, List<VisualStateRule> rules, Transform window, List<SpriteRenderer> shapes, Sprite white)
        {
            // 高亮框挂在窗口上而不是物体上：这样它不会算进物体的点击区域
            SpriteRenderer halo = CreateHalo(window, shapes, white);

            SerializedObject so = new SerializedObject(target);
            SetString(so, "id", id);
            SetInt(so, "era", (int)era);
            SetString(so, "displayName", displayName);
            SetString(so, "defaultState", defaultState);
            SetRef(so, "hitArea", target.GetComponent<BoxCollider2D>());
            SetRef(so, "hoverFrame", halo);
            WriteStateGroups(so, groups);
            WriteVisualRules(so, rules);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRoot(PuzzleTableSO table)
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "搭建解密演示");

            PuzzleBootstrap bootstrap = root.AddComponent<PuzzleBootstrap>();
            root.AddComponent<PuzzlePointerController>();

            SerializedObject so = new SerializedObject(bootstrap);
            SetRef(so, "table", table);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearDemoObjects()
        {
            EraWindow[] windows = Object.FindObjectsOfType<EraWindow>(true);
            for (int i = 0; i < windows.Length; i++)
            {
                Transform parent = windows[i].transform;

                for (int c = parent.childCount - 1; c >= 0; c--)
                {
                    GameObject child = parent.GetChild(c).gameObject;
                    if (child.name.StartsWith(InteractablePrefix) ||
                        child.name.StartsWith(CharacterPrefix) ||
                        child.name == HaloName)
                    {
                        Undo.DestroyObjectImmediate(child);
                    }
                }
            }

            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        private static EraWindow FindWindow(EraId era)
        {
            EraWindow[] windows = Object.FindObjectsOfType<EraWindow>(true);
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i].Era == era)
                {
                    return windows[i];
                }
            }

            return null;
        }

        private static SpriteRenderer Create(Transform parent, string name, Vector2 center, Vector2 size,
            Color color, Sprite white, int layer) =>
            EraWindowSceneBuilder.CreateRect(parent, name, center, size, color, ShapeOrder(layer), white);

        /// <summary>人物的形状：层级从 CharacterBase 起（比物件高，人站前面）。</summary>
        private static SpriteRenderer CreateCharacterShape(Transform parent, string name, Vector2 center, Vector2 size,
            Color color, Sprite white, int layer) =>
            EraWindowSceneBuilder.CreateRect(parent, name, center, size, color,
                PuzzleSortingOrder.CharacterBase + layer, white);

        private static List<GameObject> ToObjects(List<SpriteRenderer> shapes)
        {
            List<GameObject> objects = new List<GameObject>(shapes.Count);
            for (int i = 0; i < shapes.Count; i++)
            {
                objects.Add(shapes[i].gameObject);
            }

            return objects;
        }

        private static void SetGroupActive(StateGroup group, bool active)
        {
            if (group?.objects == null)
            {
                return;
            }

            for (int i = 0; i < group.objects.Count; i++)
            {
                if (group.objects[i] != null)
                {
                    group.objects[i].SetActive(active);
                }
            }
        }

        /// <summary>
        /// 在 parent 下面画一个包住这些形状的高亮框。
        /// 物件的高亮框挂在窗口上（物件不会动）；**人物**的要挂在人物自己身上（人会跨窗口搬，挂在窗口上就不会跟着走）。
        /// </summary>
        private static SpriteRenderer CreateHalo(Transform parent, List<SpriteRenderer> shapes, Sprite white)
        {
            bool has = false;
            Bounds bounds = default;

            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i] == null)
                {
                    continue;
                }

                if (has)
                {
                    bounds.Encapsulate(shapes[i].bounds);
                }
                else
                {
                    bounds = shapes[i].bounds;
                    has = true;
                }
            }

            if (!has)
            {
                return null;
            }

            Vector3 center = parent.InverseTransformPoint(bounds.center);
            Vector3 size = parent.InverseTransformVector(bounds.size);

            SpriteRenderer halo = EraWindowSceneBuilder.CreateRect(parent, HaloName,
                new Vector2(center.x, center.y),
                new Vector2(Mathf.Abs(size.x) + 0.22f, Mathf.Abs(size.y) + 0.22f),
                new Color(1f, 1f, 1f, 0.18f), HaloOrder, white);

            halo.enabled = false;
            return halo;
        }

        // ===================== 写私有 [SerializeField] =====================

        private static void WriteStateGroups(SerializedObject so, List<StateGroup> groups)
        {
            SerializedProperty list = Find(so, "stateGroups");

            list.arraySize = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("state").stringValue = groups[i].state;

                SerializedProperty objects = element.FindPropertyRelative("objects");
                objects.arraySize = groups[i].objects.Count;

                for (int j = 0; j < groups[i].objects.Count; j++)
                {
                    objects.GetArrayElementAtIndex(j).objectReferenceValue = groups[i].objects[j];
                }
            }
        }

        private static void WriteVisualRules(SerializedObject so, List<VisualStateRule> rules)
        {
            SerializedProperty list = Find(so, "visualRules");
            int count = rules != null ? rules.Count : 0;

            list.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("note").stringValue = rules[i].note;
                element.FindPropertyRelative("state").stringValue = rules[i].state;
                WriteConditions(element.FindPropertyRelative("conditions"), rules[i].conditions);
            }
        }

        /// <summary>
        /// 往 [SerializeReference] 列表里塞条件。
        /// 直接把 new 出来的对象赋给 managedReferenceValue 就行 —— Unity 会自己给它们分配引用 id 存进资产。
        /// </summary>
        private static void WriteConditions(SerializedProperty list, List<PuzzleCondition> conditions)
        {
            int count = conditions != null ? conditions.Count : 0;

            list.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                list.GetArrayElementAtIndex(i).managedReferenceValue = conditions[i];
            }
        }

        private static SerializedProperty Find(SerializedObject so, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[谜题] 在 {so.targetObject.GetType().Name} 上找不到字段「{field}」。" +
                               "搭建脚本和组件字段名对不上了 —— 改字段名的时候记得同步这里。");
            }

            return property;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
