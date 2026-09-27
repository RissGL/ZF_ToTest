using System.Collections.Generic;
using TMPro;
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
    /// 一键搭出**策划那张图**的场景：「交互行为对文明发展的连锁影响」。
    ///
    ///   七条链（骨针 / 火 / 狗尾巴草 / 乐器 / 羊皮纸 / 石头工具 / 住处），
    ///   每条链从石器时代一路传到信息时代，每一环是一个能点的物体；
    ///   点亮上一环，下一个时代对应的那一格就跟着亮 —— 这就是"连锁反应"。
    ///   七条链全部接上 → 信息时代的**时光机**启动（游戏主线：未来人穿越回过去）。
    ///
    /// 布局上，**同一个链号在四个时代里是同一格**：四代人看过去，同一格从暗变亮就是这条链传到这儿了。
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

        /// <summary>裂隙这一类的名字。规则表里写 `@rift` 就管住四个裂隙。</summary>
        private const string RiftCategory = "rift";

        /// <summary>链上所有节点共用的类别。规则表里写 `@chain` 就管住所有链节点。</summary>
        private const string ChainCategory = "chain";

        /// <summary>「钻木取火」那一环的类别 —— 它要拿燧石才点得动，所以**故意不在** @chain 里。</summary>
        private const string FireCategory = "fire";

        /// <summary>时光机自己的类别（它不属于任何一条链）。</summary>
        private const string MachineCategory = "machine";

        /// <summary>燧石的道具 id（抠石头工具拿到它）。</summary>
        private const string FlintItem = "flint";

        private const string FireChainId = "fire";

        /// <summary>时光机的 id（信息时代的主线）。</summary>
        private const string TimeMachineId = "time_machine";

        // ===================== 策划的那张图 =====================
        //
        // 「早期发明对后续时代的深远影响」：每条链从石器时代一路传到最后。
        // 四个时代的对应关系（策划写的是"原始 / 中世纪 / 现代 / 未来"）：
        //   石器 = 原始　蒸汽 = 中世纪与工坊　电气 = 现代　信息 = 未来
        //
        // 每一环都是一个物体，带 chain + stage 两个字段；「上一环亮了才点得动」由框架判定，
        // 所以**七条链加起来只要 3 条规则**（见 BuildRules）。

        private class ChainStage
        {
            public EraId Era;
            public string Id;
            public string Name;

            /// <summary>这一环要手里拿着东西才点得动（效果由它自己的规则负责点亮）。</summary>
            public bool NeedsItem;
        }

        private class ChainDef
        {
            public string Id;

            /// <summary>碑上写的短名（两三个字，四个时代写着同一个名字 —— 一眼看出是同一条链）。</summary>
            public string ShortName;

            public string Title;
            public Color Color;
            public ChainStage[] Stages;
        }

        private static ChainStage Stage(EraId era, string id, string name, bool needsItem = false) =>
            new ChainStage { Era = era, Id = id, Name = name, NeedsItem = needsItem };

        private static readonly ChainDef[] Chains =
        {
            // 0 骨针：原始人发现骨针 → 皮甲与盔甲的演进 → 现代衣物 → 未来服饰
            new ChainDef
            {
                Id = "needle",
                ShortName = "骨针",
                Title = "骨针：从缝皮子到未来服饰",
                Color = new Color(0.86f, 0.62f, 0.36f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Stone, "bone_needle", "骨针"),
                    Stage(EraId.Steam, "leather_armor", "皮甲与盔甲"),
                    Stage(EraId.Electric, "modern_clothes", "现代衣物"),
                    Stage(EraId.Information, "future_wearable", "未来服饰"),
                },
            },

            // 1 火：钻木取火 → 蜡烛 → 照明灯具 → 城市光网
            new ChainDef
            {
                Id = FireChainId,
                ShortName = "火",
                Title = "火：从钻木取火到城市光网",
                Color = new Color(0.95f, 0.55f, 0.22f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Stone, "fire_drill", "钻木取火", true),
                    Stage(EraId.Steam, "candle", "蜡烛"),
                    Stage(EraId.Electric, "lamp", "照明灯具"),
                    Stage(EraId.Information, "light_grid", "城市光网"),
                },
            },

            // 2 狗尾巴草：驯化 → 面包与酿造 → 成熟农作物 → 营养剂
            new ChainDef
            {
                Id = "grass",
                ShortName = "狗尾巴草",
                Title = "狗尾巴草：从驯化到营养剂",
                Color = new Color(0.62f, 0.80f, 0.34f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Stone, "wild_grass", "驯化狗尾草"),
                    Stage(EraId.Steam, "bakery", "面包与酿造"),
                    Stage(EraId.Electric, "crop_field", "成熟农作物"),
                    Stage(EraId.Information, "nutrient", "营养剂"),
                },
            },

            // 3 乐器：吟游诗人的乐器 → 现代吉他 → 打碟
            new ChainDef
            {
                Id = "music",
                ShortName = "乐器",
                Title = "乐器：从吟游诗人到打碟",
                Color = new Color(0.78f, 0.52f, 0.86f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Steam, "lute", "吟游乐器"),
                    Stage(EraId.Electric, "guitar", "现代吉他"),
                    Stage(EraId.Information, "dj_deck", "打碟台"),
                },
            },

            // 4 羊皮纸：羊皮纸 → 现代书籍 → 云端档案（策划这条末尾写着"不知道"，先接一个）
            new ChainDef
            {
                Id = "paper",
                ShortName = "羊皮纸",
                Title = "羊皮纸：从羊皮到云端档案",
                Color = new Color(0.90f, 0.84f, 0.62f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Steam, "parchment", "羊皮纸"),
                    Stage(EraId.Electric, "book", "现代书籍"),
                    Stage(EraId.Information, "cloud_archive", "云端档案"),
                },
            },

            // 5 石头工具：石器 → 中世纪武器 → 现代用品 → 义体（第一环就是抠出燧石那个）
            new ChainDef
            {
                Id = "tool",
                ShortName = "石头工具",
                Title = "石头工具：从石器到义体",
                Color = new Color(0.66f, 0.70f, 0.78f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Stone, "stone_tool", "石头工具"),
                    Stage(EraId.Steam, "medieval_weapon", "中世纪武器"),
                    Stage(EraId.Electric, "modern_goods", "现代用品"),
                    Stage(EraId.Information, "cyber_body", "义体"),
                },
            },

            // 6 住处：策划这条只写到「自我发散」，先摆一个占位节点，点它会给一句实话
            new ChainDef
            {
                Id = "shelter",
                ShortName = "住处",
                Title = "住处：策划这条还没想好（自我发散）",
                Color = new Color(0.58f, 0.54f, 0.62f, 1f),
                Stages = new[]
                {
                    Stage(EraId.Stone, "shelter", "住处"),
                },
            },
        };

        // 场景尺寸跟着窗口走（窗口 9.8 × 5.88）。槽位固定：第几条链就永远在哪一格。
        // 右边要给裂隙留出位置（裂隙在 x = 4.05，宽约 1.2），所以最后一列只到 2.3。
        private const float SlotStepX = 2.10f;
        private const float SlotOriginX = -4.00f;
        private const float SlotTopY = 1.58f;
        private const float SlotBottomY = -0.68f;
        private const float RiftX = 4.10f;

        /// <summary>人物的站位：偏右一点站 —— 地景左边那条留给"要接上：…"的提示文字。</summary>
        private const float SeatX = 1.85f;

        private const float SeatY = -2.30f;
        private const float SeatSpacing = 1.05f;

        private static Vector2 SlotOf(int chainIndex) => new Vector2(
            SlotOriginX + (chainIndex % 4) * SlotStepX,
            chainIndex < 4 ? SlotTopY : SlotBottomY);

        // 层级：内容 0 / 地景 5 / 时代序号点 7 / 边框 10 / 锁-对勾 20（EraWindowSceneBuilder 定的）
        private const int HaloOrder = PuzzleSortingOrder.Halo;

        /// <summary>
        /// ★ 同一个东西里**重叠的形状必须给递增的层级**（layer 参数）。
        /// 层级相同又重叠时画的顺序是不确定的 —— 踩过的坑：壁炉本体和火苗同层级，
        /// 本体有时候盖在火苗上，表现成"石器时代点了火、壁炉却没火；有时候又有火；有时候只有下面一个火苗"。
        /// </summary>
        private static int ShapeOrder(int layer) => PuzzleSortingOrder.ObjectBase + layer;

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

            // 工程里唯一的中文字体（思源宋体）：碑上要写"这是哪条链的第几环"，没字谁也看不懂
            TMP_FontAsset font = EraWindowSceneBuilder.LoadFontAsset();

            ClearDemoObjects();

            // 规则表：老版本的表里没有人物/裂隙那些规则（或没有"点名"），场景搭好了也点不动 —— 检测到就直接重建。
            // 还有几种"写法变了"的旧表也要认出来：人物规则**按人**一条条写、裂隙规则**按时代**一条条写、
            // 没有「链」这套（`@chain`）。平时表是新的就不动它，免得把你在 Inspector 里改过的规则冲掉。
            PuzzleTableSO existing = AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(TablePath);
            bool stale = existing != null &&
                         (!HasRuleFor(existing, ChainCategoryRef) ||
                          !HasRuleFor(existing, RiftCategoryRef) ||
                          !HasRuleFor(existing, CharacterCategoryRef) ||
                          !HasEffect<LightChainStageEffect>(existing) ||
                          !HasStepPuzzle(existing) ||
                          HasRedundantFallbackRule(existing));

            PuzzleTableSO table = EnsureTable(stale);
            if (stale)
            {
                Debug.Log("[谜题] 演示规则表是旧版本的（没有「链」这套，或者还是按人/按时代一条条写的老写法），" +
                          "已按策划那七条链重建 —— 你在 Inspector 里改过的演示规则被覆盖了。");
            }

            // ---- 七条链：每条链的每一环 = 一个物体，摆在**固定的槽位**上 ----
            // 槽位跟着链的序号走（第 3 条链永远在同一格），所以四代人看过去，
            // 同一格从暗变亮 = 这条链传到这个时代了 —— 连锁关系不用文字也看得见。
            for (int c = 0; c < Chains.Length; c++)
            {
                ChainDef chain = Chains[c];
                Vector2 slot = SlotOf(c);

                for (int s = 0; s < chain.Stages.Length; s++)
                {
                    ChainStage stage = chain.Stages[s];
                    Transform window = windows[(int)stage.Era].transform;

                    // 需要道具才点得动的那一环（钻木取火）不进 @chain 这一类，它有自己的规则
                    string category = chain.Id == FireChainId && s == 0 ? FireCategory : ChainCategory;

                    BuildChainNode(window, stage.Id, stage.Era, stage.Name, chain, s + 1, slot, category,
                        stage.NeedsItem ? "得先有块能打火的石头。" : "", white, font);
                }
            }

            // 地景那条带上写一行"这一代该做什么"：不写的话玩家进了时代也不知道从哪儿下手
            BuildEraHints(windows, font);

            // 主线：信息时代的时光机（未来人发明时光机 → 穿越过去）
            BuildTimeMachine(windows[(int)EraId.Information].transform, white, font);

            // 每个时代一个时间裂隙
            for (int i = 0; i < windows.Count; i++)
            {
                BuildRift(windows[i].transform, EraCatalog.All[i].id, white, font);
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
                float x = (slot - (eraCounts[eraIndex] - 1) * 0.5f) * SeatSpacing;

                BuildCharacter(windows[eraIndex].transform, i, new Vector3(SeatX + x, SeatY, 0f), white);
            }

            BuildRoot(table);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[谜题] 连锁测试场景搭好了：{Chains.Length} 条链 × 各时代一环，一共 {CountStages()} 个链节点 + 时光机 + 4 个时间裂隙 + {CharacterIds.Length} 个人物。\n" +
                      "  玩法（按 Play）：\n" +
                      "  1. 进石器时代 → 点**石头工具**拿「燧石」→ Tab 拿在手里 → 点**钻木取火**（第一环亮了）\n" +
                      "  2. 点火之后：石头工具/钻木取火那一格亮起来 → 回全景看，蒸汽时代对应的格子跟着亮了\n" +
                      "  3. 进蒸汽时代 → 点亮着的那一环 → 它点亮电气时代对应的格子……**七条链各自往下传**\n" +
                      "  4. 同一格的链条在四个时代里是**纵向对齐**的（第 3 条链永远在第 3 格），所以一眼看得出「这条链传到哪了」\n" +
                      "  5. 七条链全部接上 → 信息时代的**时光机**启动（那是主线：未来人穿越回过去）\n" +
                      "  6. 人物照旧：点人物 = 点名/取消点名，点时间裂隙 = 送他走（裂隙要等火点着才开）\n" +
                      $"  画面上：每一格写着「链名 + 这一代叫什么 + 第几环」，**轮到点的那一格自己会亮白边框**。\n" +
                      $"  规则表在 {TablePath}：10 条规则管住 {CountStages() + 5} 个物体 —— 加一条链不用补规则。");
        }

        /// <summary>
        /// 每个时代地景带上那一行"这一代要接上什么"。
        /// 用的是**碑上写的那个短名**（骨针 / 火 / …），所以这行字和画面上那几块碑一一对得上；
        /// 太长就折成两行（地景带着得下，而且右半留给站在那儿的角色）。
        /// </summary>
        private static void BuildEraHints(List<EraWindow> windows, TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            for (int e = 0; e < EraCatalog.All.Length; e++)
            {
                EraId era = EraCatalog.All[e].id;
                List<string> names = new List<string>();

                for (int c = 0; c < Chains.Length; c++)
                {
                    for (int s = 0; s < Chains[c].Stages.Length; s++)
                    {
                        if (Chains[c].Stages[s].Era == era)
                        {
                            names.Add(Chains[c].ShortName);
                            break;
                        }
                    }
                }

                if (names.Count == 0)
                {
                    continue;
                }

                // 写在**边框下面**那一行：地景带上有角色站着，往那儿塞字会打架；
                // 边框下面这条空带够宽（9.8），放大到 0.24 也放得下。
                List<string> lines = Wrap("要接上：", names, 19);
                Color color = new Color(0.94f, 0.92f, 0.86f, 1f);

                for (int i = 0; i < lines.Count; i++)
                {
                    EraWindowSceneBuilder.CreateLabel(windows[e].transform, "EraHint" + i, lines[i],
                        new Vector2(-4.62f, -2.50f - i * 0.30f), 0.22f, color, font,
                        EraWindowSceneBuilder.TextOrder, TextAnchor.MiddleLeft);
                }
            }
        }

        /// <summary>把一串名字按" · "拼起来，超长了就折行（按字数粗略估宽，中文差不多一个字一格）。</summary>
        private static List<string> Wrap(string prefix, List<string> names, int maxChars)
        {
            List<string> lines = new List<string>();
            string current = prefix;

            for (int i = 0; i < names.Count; i++)
            {
                string piece = (current.EndsWith("：") || current.EndsWith(" ") ? "" : " · ") + names[i];

                if (current.Length + piece.Length > maxChars && current.Length > prefix.Length)
                {
                    lines.Add(current);
                    current = "  " + names[i];
                    continue;
                }

                current += piece;
            }

            lines.Add(current);
            return lines;
        }

        /// <summary>链上一共几个节点（日志用）。</summary>
        private static int CountStages()
        {
            int count = 0;
            for (int i = 0; i < Chains.Length; i++)
            {
                count += Chains[i].Stages.Length;
            }

            return count;
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

        // ===================== 链上的一个节点 =====================

        /// <summary>
        /// 链上的一环：一块"碑"，三个状态（**没轮到 / 轮到你了 / 已点亮**）。
        ///
        /// 三个问题都得在画面上答出来，不然"全都一样，谁知道怎么玩"：
        ///   ① 这格是哪条链的？→ **链色**（没点亮时也带着自己的色）+ 碑上写着链名
        ///   ② 是第几环？→ 碑上写「第 N 环」+ 底下一排小点
        ///   ③ 现在该点哪个？→ 轮到的那一格**自己亮边框**（state = "ready"）
        /// 同一个链号在四个时代里永远是**同一格**，所以四代人看过去，同一格从暗到亮就是这条链传过去了。
        /// </summary>
        private static void BuildChainNode(Transform window, string id, EraId era, string displayName,
            ChainDef chain, int stage, Vector2 slot, string category, string defaultFeedback, Sprite white, TMP_FontAsset font)
        {
            Interactable target = CreateInteractable(window, id, era, displayName);
            target.transform.localPosition = new Vector3(slot.x, slot.y, 0f);

            // 碑要够大：上面三行字（链名 / 这一代叫什么 / 第几环）在全景下也得看得清
            const float width = 1.60f;
            const float height = 1.42f;
            float halfHeight = height * 0.5f;

            Color ink = new Color(0.10f, 0.11f, 0.14f, 1f);

            // 没点亮时**也带着链色**（压暗到 22%）：不然四代七块碑长得一模一样，谁也认不出哪条是哪条
            Color dimBody = Color.Lerp(ink, chain.Color, 0.22f);
            Color dimBar = Color.Lerp(ink, chain.Color, 0.55f);
            Color dimEdge = Color.Lerp(ink, chain.Color, 0.35f);

            Color litBody = Color.Lerp(ink, chain.Color, 0.55f);
            Color litBar = chain.Color;
            Color litDot = Color.Lerp(chain.Color, Color.white, 0.45f);

            List<SpriteRenderer> dim = new List<SpriteRenderer>
            {
                Create(target.transform, "DimGlow", Vector2.zero, new Vector2(width + 0.16f, height + 0.16f),
                    new Color(dimEdge.r, dimEdge.g, dimEdge.b, 0.35f), white, 0),
                Create(target.transform, "DimBody", Vector2.zero, new Vector2(width, height), dimBody, white, 1),
                Create(target.transform, "DimBar", new Vector2(0f, halfHeight - 0.12f), new Vector2(width, 0.16f),
                    dimBar, white, 2),
            };

            // 轮到它了：亮起**边框**（不填亮色，免得和"已经点亮"混在一起）
            List<SpriteRenderer> ready = new List<SpriteRenderer>
            {
                Create(target.transform, "ReadyGlow", Vector2.zero, new Vector2(width + 0.42f, height + 0.42f),
                    new Color(chain.Color.r, chain.Color.g, chain.Color.b, 0.22f), white, 0),
                Create(target.transform, "ReadyBody", Vector2.zero, new Vector2(width, height),
                    Color.Lerp(ink, chain.Color, 0.30f), white, 1),
                Create(target.transform, "ReadyBar", new Vector2(0f, halfHeight - 0.12f), new Vector2(width, 0.16f),
                    dimBar, white, 2),
                Create(target.transform, "ReadyEdgeT", new Vector2(0f, halfHeight), new Vector2(width + 0.14f, 0.09f),
                    Color.white, white, 3),
                Create(target.transform, "ReadyEdgeB", new Vector2(0f, -halfHeight), new Vector2(width + 0.14f, 0.09f),
                    Color.white, white, 3),
                Create(target.transform, "ReadyEdgeL", new Vector2(-width * 0.5f, 0f), new Vector2(0.09f, height + 0.14f),
                    Color.white, white, 3),
                Create(target.transform, "ReadyEdgeR", new Vector2(width * 0.5f, 0f), new Vector2(0.09f, height + 0.14f),
                    Color.white, white, 3),
            };

            List<SpriteRenderer> lit = new List<SpriteRenderer>
            {
                Create(target.transform, "LitGlow", Vector2.zero, new Vector2(width + 0.34f, height + 0.34f),
                    new Color(chain.Color.r, chain.Color.g, chain.Color.b, 0.30f), white, 0),
                Create(target.transform, "LitBody", Vector2.zero, new Vector2(width, height), litBody, white, 1),
                Create(target.transform, "LitBar", new Vector2(0f, halfHeight - 0.12f), new Vector2(width, 0.20f),
                    litBar, white, 2),
            };

            // 第几环：底下那一排小点（三组都画，颜色不同，切状态时点不会跳）
            for (int i = 0; i < chain.Stages.Length; i++)
            {
                float x = (i - (chain.Stages.Length - 1) * 0.5f) * 0.24f;
                bool mine = i == stage - 1;
                float size = mine ? 0.22f : 0.16f;

                dim.Add(Create(target.transform, $"DimDot_{i}", new Vector2(x, -halfHeight + 0.19f),
                    new Vector2(size, size), dimEdge, white, 3));
                ready.Add(Create(target.transform, $"ReadyDot_{i}", new Vector2(x, -halfHeight + 0.19f),
                    new Vector2(size, size), mine ? Color.white : dimEdge, white, 4));
                lit.Add(Create(target.transform, $"LitDot_{i}", new Vector2(x, -halfHeight + 0.19f),
                    new Vector2(size, size), i <= stage - 1 ? litDot : Color.Lerp(ink, chain.Color, 0.2f), white, 3));
            }

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = PuzzleStates.Default, objects = ToObjects(dim) },
                new StateGroup { state = "ready", objects = ToObjects(ready) },
                new StateGroup { state = "lit", objects = ToObjects(lit) },
            };

            SetGroupActive(groups[1], false);
            SetGroupActive(groups[2], false);

            // 外观规则：**两条管所有链节点**（不用给每个节点各写一条 flag 条件）
            //   lit 在前：已经点亮就用 lit；否则"轮到你了"才亮边框
            //
            // 要道具才点得动的那一环（钻木取火）例外：它的"轮到"不看上一环（它是第 1 环），
            // 而是看**手里有没有燧石** —— 亮边框的含义统一是"现在点它有反应"。
            List<VisualStateRule> visualRules = new List<VisualStateRule>
            {
                new VisualStateRule
                {
                    note = "这一环被点亮了",
                    state = "lit",
                    conditions = Conditions(new ChainStageLitCondition()),
                },
                category == FireCategory
                    ? new VisualStateRule
                    {
                        note = "手里有燧石了 → 亮边框提示可以点它了",
                        state = "ready",
                        conditions = Conditions(Item(FlintItem)),
                    }
                    : new VisualStateRule
                    {
                        note = "轮到这一环了（上一环亮了、它还没亮）→ 亮边框提示玩家点它",
                        state = "ready",
                        conditions = Conditions(new ChainStageReadyCondition()),
                    },
            };

            List<SpriteRenderer> all = new List<SpriteRenderer>(dim);
            all.AddRange(ready);
            all.AddRange(lit);

            Finish(target, id, era, displayName, PuzzleStates.Default, groups, visualRules, window, all, white,
                defaultFeedback: defaultFeedback, category: category, chain: chain.Id, stage: stage);

            BuildNodeLabels(target.transform, chain, stage, displayName, font);
        }

        /// <summary>碑上的字：链名（哪一条）+ 这一代叫什么（这一环是什么）+ 第几环。</summary>
        private static void BuildNodeLabels(Transform parent, ChainDef chain, int stage, string stageName, TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            // 链名带链色：四个时代同一格写着同一个名字，一眼看出这是同一条链
            EraWindowSceneBuilder.CreateLabel(parent, "ChainName", chain.ShortName,
                new Vector2(0f, 0.44f), 0.30f, Color.Lerp(chain.Color, Color.white, 0.42f), font,
                EraWindowSceneBuilder.TextOrder);

            EraWindowSceneBuilder.CreateLabel(parent, "StageName", stageName,
                new Vector2(0f, 0.11f), 0.24f, new Color(0.93f, 0.93f, 0.95f, 1f), font,
                EraWindowSceneBuilder.TextOrder);

            EraWindowSceneBuilder.CreateLabel(parent, "StageNumber", $"第 {stage} 环",
                new Vector2(0f, -0.19f), 0.20f, Color.Lerp(chain.Color, Color.white, 0.20f), font,
                EraWindowSceneBuilder.TextOrder);
        }

        /// <summary>主线那个东西：信息时代的时光机（未来人发明时光机 → 穿越过去）。</summary>
        private static void BuildTimeMachine(Transform window, Sprite white, TMP_FontAsset font)
        {
            Interactable target = CreateInteractable(window, TimeMachineId, EraId.Information, "时光机");
            target.transform.localPosition = new Vector3(SlotOriginX + 3 * SlotStepX, SlotBottomY, 0f);

            Color frame = new Color(0.55f, 0.78f, 0.92f, 1f);
            Color core = new Color(0.38f, 0.86f, 0.92f, 1f);
            Color dimBody = new Color(0.15f, 0.17f, 0.22f, 1f);

            List<SpriteRenderer> idle = new List<SpriteRenderer>
            {
                Create(target.transform, "CaseBody", Vector2.zero, new Vector2(1.5f, 1.2f), dimBody, white, 0),
                Create(target.transform, "CaseFrameL", new Vector2(-0.68f, 0f), new Vector2(0.14f, 1.2f), frame, white, 1),
                Create(target.transform, "CaseFrameR", new Vector2(0.68f, 0f), new Vector2(0.14f, 1.2f), frame, white, 1),
                Create(target.transform, "CaseFrameT", new Vector2(0f, 0.54f), new Vector2(1.5f, 0.14f), frame, white, 1),
                Create(target.transform, "CaseFrameB", new Vector2(0f, -0.54f), new Vector2(1.5f, 0.14f), frame, white, 1),
            };

            List<SpriteRenderer> running = new List<SpriteRenderer>
            {
                Create(target.transform, "RunGlow", Vector2.zero, new Vector2(2.1f, 1.8f),
                    new Color(core.r, core.g, core.b, 0.26f), white, 0),
                Create(target.transform, "RunBody", Vector2.zero, new Vector2(1.5f, 1.2f), dimBody, white, 1),
                Create(target.transform, "RunCore", Vector2.zero, new Vector2(0.86f, 0.62f), core, white, 2),
                Create(target.transform, "RunFrameL", new Vector2(-0.68f, 0f), new Vector2(0.14f, 1.2f), frame, white, 3),
                Create(target.transform, "RunFrameR", new Vector2(0.68f, 0f), new Vector2(0.14f, 1.2f), frame, white, 3),
                Create(target.transform, "RunFrameT", new Vector2(0f, 0.54f), new Vector2(1.5f, 0.14f), frame, white, 3),
                Create(target.transform, "RunFrameB", new Vector2(0f, -0.54f), new Vector2(1.5f, 0.14f), frame, white, 3),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = "idle", objects = ToObjects(idle) },
                new StateGroup { state = "running", objects = ToObjects(running) },
            };

            SetGroupActive(groups[1], false);

            // 七条链的终章都亮了 → 时光机自己转起来（外观规则读的就是那七个 flag）
            List<PuzzleCondition> ready = new List<PuzzleCondition>();
            for (int i = 0; i < Chains.Length; i++)
            {
                ChainDef chain = Chains[i];
                ready.Add(FlagOn(PuzzleChain.Flag(chain.Id, chain.Stages.Length)));
            }

            List<VisualStateRule> visualRules = new List<VisualStateRule>
            {
                new VisualStateRule { note = "七条链全接上了", state = "running", conditions = ready },
            };

            List<SpriteRenderer> all = new List<SpriteRenderer>(idle);
            all.AddRange(running);

            Finish(target, TimeMachineId, EraId.Information, "时光机", "idle", groups, visualRules, window, all, white,
                defaultFeedback: "时光机只亮了一半 —— 七条链得全部接上。", category: MachineCategory);

            if (font != null)
            {
                EraWindowSceneBuilder.CreateLabel(target.transform, "MachineName", "时光机",
                    new Vector2(0f, 0.98f), 0.32f, new Color(0.72f, 0.93f, 1f, 1f), font,
                    EraWindowSceneBuilder.TextOrder);

                EraWindowSceneBuilder.CreateLabel(target.transform, "MachineHint", "主线：七条链全接上就启动",
                    new Vector2(0f, -0.92f), 0.20f, new Color(0.64f, 0.82f, 0.92f, 1f), font,
                    EraWindowSceneBuilder.TextOrder);
            }
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

            // 台词挂在人物自己身上：规则表里那条「任意人物：点名」是通配的，
            // 说不了每个人不同的话 —— 用 {目标说} 引用这句就行。
            SetString(so, "speech", CharacterLines[index]);

            SetRef(so, "hitArea", hitArea);
            SetRef(so, "hoverFrame", halo);

            // 站位跟着新窗口尺寸走：人站在地景那条带上，同代多人按间距自动分开
            SetFloat(so, "seatX", SeatX);
            SetFloat(so, "seatSpacing", SeatSpacing);

            WriteStateGroups(so, groups);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 人名：站在地景带上，头顶写名字（标签是子节点，人搬走了字跟着走）
            TMP_FontAsset font = EraWindowSceneBuilder.LoadFontAsset();
            if (font != null)
            {
                EraWindowSceneBuilder.CreateLabel(go.transform, "Name", CharacterNames[index],
                    new Vector2(0f, 0.60f), 0.24f, new Color(0.95f, 0.94f, 0.91f, 1f), font,
                    EraWindowSceneBuilder.TextOrder);
            }
        }

        /// <summary>
        /// 一个时代里的时间裂隙。做成一开一闭两个状态：
        /// 火种出现了（链 2 第 1 环的 flag）它就张开，点它把这个时代的人送到下一个时代。
        /// 位置在窗口右侧竖着一条，和左边四列的链节点不打架。
        /// </summary>
        private static void BuildRift(Transform window, EraId era, Sprite white, TMP_FontAsset font)
        {
            string id = RiftId(era);
            Interactable target = CreateInteractable(window, id, era, "时间裂隙");
            target.transform.localPosition = new Vector3(RiftX, 0f, 0f);

            List<SpriteRenderer> closed = new List<SpriteRenderer>
            {
                Create(target.transform, "RiftDim", Vector2.zero, new Vector2(0.42f, 2.6f), RiftDim, white, 0),
                Create(target.transform, "RiftDimEdgeA", new Vector2(-0.08f, 0.9f), new Vector2(0.14f, 0.7f), RiftSoft, white, 1),
                Create(target.transform, "RiftDimEdgeB", new Vector2(0.10f, -0.5f), new Vector2(0.14f, 0.6f), RiftSoft, white, 2),
            };

            // 光晕 0 < 裂缝 1/2/3/4 —— 递增，重叠也不会画得不确定
            List<SpriteRenderer> open = new List<SpriteRenderer>
            {
                Create(target.transform, "RiftGlow", Vector2.zero, new Vector2(1.15f, 2.9f), RiftSoft, white, 0),
                Create(target.transform, "CrackA", new Vector2(-0.06f, 1.05f), new Vector2(0.15f, 0.62f), RiftBright, white, 1),
                Create(target.transform, "CrackB", new Vector2(0.09f, 0.35f), new Vector2(0.15f, 0.72f), RiftBright, white, 2),
                Create(target.transform, "CrackC", new Vector2(-0.08f, -0.30f), new Vector2(0.15f, 0.58f), RiftBright, white, 3),
                Create(target.transform, "CrackD", new Vector2(0.07f, -0.95f), new Vector2(0.15f, 0.66f), RiftBright, white, 4),
            };

            List<StateGroup> groups = new List<StateGroup>
            {
                new StateGroup { state = "closed", objects = ToObjects(closed) },
                new StateGroup { state = "open", objects = ToObjects(open) },
            };

            SetGroupActive(groups[1], false);

            // 四个时代的裂隙读的是同一个全局 flag，所以火点着时它们会一起张开 ——
            // 这本身就是"状态共享"的又一个例子。正式内容里换成每个时代自己的通关 flag。
            List<VisualStateRule> rules = new List<VisualStateRule>
            {
                new VisualStateRule
                {
                    note = "火种出现了，裂隙就能感觉到",
                    state = "open",
                    conditions = Conditions(FlagOn(FireLitFlag)),
                },
            };

            List<SpriteRenderer> all = new List<SpriteRenderer>(closed);
            all.AddRange(open);

            // 类别 = rift：规则表里一条「@rift」就管四个裂隙（不用每个时代写一条规则）。
            // 默认提示 = 裂隙还没开时说的一句话（没有规则命中时就说它）。
            Finish(target, id, era, "时间裂隙", "closed", groups, rules, window, all, white,
                defaultFeedback: "裂隙还闭着。得先让这个年头的火点起来。",
                category: RiftCategory);

            if (font != null)
            {
                EraWindowSceneBuilder.CreateLabel(target.transform, "RiftName", "时间裂隙",
                    new Vector2(0f, -1.78f), 0.26f, new Color(0.82f, 0.95f, 1f, 1f), font,
                    EraWindowSceneBuilder.TextOrder);
            }
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

        /// <summary>表里有没有步骤式谜题。用来判断这张表是不是旧版本搭出来的。</summary>
        private static bool HasStepPuzzle(PuzzleTableSO table)
        {
            if (table == null || table.puzzles == null)
            {
                return false;
            }

            for (int i = 0; i < table.puzzles.Count; i++)
            {
                if (table.puzzles[i] != null && table.puzzles[i].mode == PuzzleMode.Steps)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 表里还留着「纯兜底的空手点规则」吗 —— 老演示版本给每个物件都单拉了一条
        /// 「已经抠过了 / 空手点柴堆 / 还是冷的 / 裂隙还闭着」这种只有一句话的规则。
        /// 新演示一律把它们写进规则的 elseFeedback 或物件的默认提示，不再占一行。
        /// 判断口径和编辑器校验（PuzzleEditorScan）是同一个，免得两边说法不一样。
        /// </summary>
        private static bool HasRedundantFallbackRule(PuzzleTableSO table)
        {
            if (table == null || table.interactionRules == null)
            {
                return false;
            }

            for (int i = 0; i < table.interactionRules.Count; i++)
            {
                if (PuzzleEditorScan.IsOnlyFeedback(table.interactionRules[i]) &&
                    PuzzleEditorScan.FindCoveringRuleAbove(table.interactionRules, i) >= 0)
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
            const string Flint = "flint";
            const string StoneTool = "stone_tool";
            const string FireDrill = "fire_drill";

            // ★ 11 条规则管住：七条链（19 个链节点）+ 时光机 + 4 个裂隙 + 5 个人物。
            //
            // 三套"一次管一批"的写法叠起来才是这个数：
            //   · 目标写类别：`@chain` 一条管所有链节点、`@rift` 一条管四个裂隙、`@character` 一条管五个人物
            //   · 效果引用"被点的那个自己"：`@self` = 点名它自己；`LightChainStageEffect` = 点亮它那一环
            //   · 时代写"被点目标所在时代 / 它的下一个"：不用按时代写四遍
            // 所以**加一条链（哪怕四个时代各一环）不用补一条规则**。
            List<InteractionRule> rules = new List<InteractionRule>
            {
                // ---- 有具体内容的两条：石头工具抠燧石、用燧石钻木取火 ----
                new InteractionRule
                {
                    note = "石头工具：抠下一片燧石（链 6 第 1 环）",
                    targetId = StoneTool,
                    verb = Verb.Interact,
                    conditions = Conditions(Flag("flint_taken", FlagOp.Equals, 0f)),
                    effects = Effects(
                        new GiveItemEffect { itemId = Flint },
                        new SetFlagEffect { flag = "flint_taken", value = 1f },
                        new LightChainStageEffect(),
                        new FeedbackEffect { message = "你从石头工具上撬下一片燧石。" }),
                },
                new InteractionRule
                {
                    note = "钻木取火：用燧石打火（链 2 第 1 环）",
                    targetId = FireDrill,
                    verb = Verb.UseItem,
                    itemId = Flint,
                    conditions = Conditions(Item(Flint)),
                    effects = Effects(
                        new ConsumeItemEffect { itemId = Flint },
                        new LightChainStageEffect(),
                        new FeedbackEffect { message = "火星落进干草，火起来了 —— 从这一刻起，后面几个时代都会跟着变。" }),
                    elseFeedback = "手里没有能打火的东西。",
                },

                // ---- 链条本身：三条管住所有链节点 ----
                // 顺序不能反：从"最特殊"到"最通用"，最后那条无条件的必须垫底。
                new InteractionRule
                {
                    note = "链：上一环亮了 → 点亮这一环（所有链节点共用这一条）",
                    targetId = ChainCategoryRef,
                    verb = Verb.Interact,
                    conditions = Conditions(
                        new ChainStageReadyCondition(),
                        new NotCondition { item = new ChainStageLitCondition() }),
                    effects = Effects(
                        new LightChainStageEffect(),
                        new FeedbackEffect { message = "「{目标名}」接上了上一环 —— 这条链传到{这里}了。" }),
                    // 「已经亮着了」不单拉一条规则 —— 兜底话写在这儿（下面那条 Not(Ready) 会先接住"还接不上"）
                    elseFeedback = "「{目标名}」已经亮着了。",
                },
                new InteractionRule
                {
                    note = "链：上一环还没亮",
                    targetId = ChainCategoryRef,
                    verb = Verb.Interact,
                    conditions = Conditions(new NotCondition { item = new ChainStageReadyCondition() }),
                    effects = Effects(
                        new FeedbackEffect { message = "「{目标名}」还接不上 —— 得先把上一环点亮。" }),
                },

                // ---- 主线：时光机（七条链全接上才启动）----
                new InteractionRule
                {
                    note = "主线：时光机（未来人发明时光机 → 穿越过去）",
                    targetId = TimeMachineId,
                    verb = Verb.Interact,
                    conditions = AllChainsDone(),
                    effects = Effects(
                        new FeedbackEffect { message = "七条链全部接上了。时光机转起来 —— 你走进了光里。" }),
                    // 还差几条链时不单拉规则，兜底话写在这儿
                    elseFeedback = "时光机只亮了一半 —— 七条链得全部接上。",
                },

                // ---- 人物：一条管所有人物。点他 = 点名（再点一次取消）----
                // 顺序不能反：先判"点名的就是他"，否则第一条会把第二条永远挡住。
                new InteractionRule
                {
                    note = "人物：已经点着他了 → 再点一次取消",
                    targetId = CharacterCategoryRef,
                    verb = Verb.Interact,
                    conditions = Conditions(new SelectedCharacterCondition { characterId = PuzzleRef.Self }),
                    effects = Effects(
                        new SelectCharacterEffect(),
                        new FeedbackEffect { message = "取消点名{目标名}。" }),
                },
                new InteractionRule
                {
                    note = "人物：点名（再去点时间裂隙他才走）",
                    targetId = CharacterCategoryRef,
                    verb = Verb.Interact,
                    conditions = Conditions(),
                    effects = Effects(
                        new SelectCharacterEffect { characterId = PuzzleRef.Self },
                        new FeedbackEffect { message = "点名{目标名}，去点时间裂隙送他走。\n{目标说}" }),
                },
            };

            // ---- 时间裂隙：三条管四个裂隙。**送到哪 = 这个裂隙所在时代的下一个**，所以不用按时代写 ----
            // 「裂隙开着」的门槛就是链 2 的第 1 环：火种出现了，裂隙才感觉得到（跨时代联动）。
            // ① 点名了人、而且那个人就在这个裂隙的时代 → 只送他一个人，并且串上动画
            rules.Add(new InteractionRule
            {
                note = "裂隙：只送点名的那个人（带离场/到场动画）",
                targetId = RiftCategoryRef,
                verb = Verb.Interact,
                conditions = Conditions(
                    FlagOn(FireLitFlag),
                    new SelectedCharacterInEraCondition { eraRef = EraRef.TargetEra }),
                effects = Effects(
                    new PlayCharacterAnimationEffect { characterId = "", clip = "leave" },
                    new MoveSelectedCharacterEffect { eraRef = EraRef.TargetEraNext, delayBefore = 0.55f },
                    new PlayCharacterAnimationEffect { characterId = "", clip = "arrive" },
                    new FeedbackEffect { message = "{人物名}一个人跨进了{下一个}。" }),
            });

            // ② 没点名（或点名的人不在这个时代）而这里有人 → 整个时代一起走
            rules.Add(new InteractionRule
            {
                note = "裂隙：整个时代一起走",
                targetId = RiftCategoryRef,
                verb = Verb.Interact,
                conditions = Conditions(
                    FlagOn(FireLitFlag),
                    new CharacterCountInEraCondition
                    {
                        eraRef = EraRef.TargetEra,
                        op = FlagOp.GreaterOrEqual,
                        count = 1f,
                    }),
                effects = Effects(
                    new MoveEraCharactersEffect { fromEraRef = EraRef.TargetEra, eraRef = EraRef.TargetEraNext },
                    new FeedbackEffect { message = "{这里}的人一起跨进了{下一个}。" }),
            });

            // ③ 裂隙开着但这个时代已经没人了。
            //    这句**不能**并到上面两条的 elseFeedback 里 —— 兜底话取的是"第一条匹配但条件不满足"的那条，
            //    那样"火还没点着"和"这个时代没人"会共用一句话。所以它单独一条，条件就是"火点着了"。
            rules.Add(new InteractionRule
            {
                note = "裂隙：开着，但这个时代没人",
                targetId = RiftCategoryRef,
                verb = Verb.Interact,
                conditions = Conditions(FlagOn(FireLitFlag)),
                effects = Effects(new FeedbackEffect { message = "{这里}里已经没有人了。" }),
            });

            return rules;
        }

        /// <summary>「裂隙」这一类。规则写类别 = 一条管四个裂隙；类别名写在裂隙物体上（见 BuildRift）。</summary>
        private const string RiftCategoryRef = "@" + RiftCategory;

        /// <summary>链上所有节点这一类。规则写 `@chain` = 一条管所有链节点。</summary>
        private const string ChainCategoryRef = "@" + ChainCategory;

        /// <summary>人物类别是框架级的（CharacterView 自动属于它）。</summary>
        private const string CharacterCategoryRef = "@" + PuzzleCategories.Character;

        /// <summary>「火种出现了」这个 flag —— 就是链 2 的第 1 环。裂隙开不开看它。</summary>
        private static readonly string FireLitFlag = PuzzleChain.Flag(FireChainId, 1);

        /// <summary>七条链的终章 flag 全满足（时光机的门槛，也是主线的完成条件）。</summary>
        private static List<PuzzleCondition> AllChainsDone()
        {
            List<PuzzleCondition> conditions = new List<PuzzleCondition>();

            for (int i = 0; i < Chains.Length; i++)
            {
                ChainDef chain = Chains[i];
                conditions.Add(FlagOn(PuzzleChain.Flag(chain.Id, chain.Stages.Length)));
            }

            return conditions;
        }

        private static List<PuzzleDefinition> BuildPuzzles()
        {
            List<PuzzleDefinition> puzzles = new List<PuzzleDefinition>();

            // ---- 每条链一个步骤式谜题：一步 = 一环，做一环勾一步 ----
            for (int i = 0; i < Chains.Length; i++)
            {
                ChainDef chain = Chains[i];
                List<PuzzleStep> steps = new List<PuzzleStep>();

                for (int s = 0; s < chain.Stages.Length; s++)
                {
                    ChainStage stage = chain.Stages[s];
                    string flag = PuzzleChain.Flag(chain.Id, s + 1);

                    steps.Add(new PuzzleStep
                    {
                        id = "s" + (s + 1),
                        title = $"{EraTitle(stage.Era)}：{stage.Name}",
                        conditions = Conditions(FlagOn(flag)),
                    });
                }

                puzzles.Add(new PuzzleDefinition
                {
                    id = "P_chain_" + chain.Id,
                    title = chain.Title,
                    era = chain.Stages[0].Era,
                    mode = PuzzleMode.Steps,
                    stepsInOrder = true,
                    steps = steps,
                    onSolved = Effects(new FeedbackEffect
                    {
                        message = $"（连锁完成）「{chain.Title}」这条链从{EraTitle(chain.Stages[0].Era)}一路传到了{EraTitle(chain.Stages[chain.Stages.Length - 1].Era)}。",
                    }),
                });
            }

            // ---- 每个时代一个主线谜题：这个时代该点的几环都点亮 = 这个时代通关 ----
            // 信息时代不在这儿做：它的主线是「时光机」（游戏主线，见下面那个七步谜题）。
            for (int e = 0; e < EraCatalog.All.Length; e++)
            {
                EraId era = EraCatalog.All[e].id;
                List<PuzzleCondition> done = EraProgressFlags(era);

                if (done.Count == 0 || era == EraId.Information)
                {
                    continue;
                }

                puzzles.Add(new PuzzleDefinition
                {
                    id = "P_era_" + era.ToString().ToLowerInvariant(),
                    title = $"{EraTitle(era)}：这一代该接的都接上了",
                    era = era,
                    conditions = done,
                    onSolved = Effects(new FeedbackEffect
                    {
                        message = $"（{EraTitle(era)}通关）这一代的连锁都接上了 —— 它们会继续往下一个时代传。",
                    }),
                    isMainPuzzle = true,   // 主线：解开 = 这个时代通关，窗口亮对勾
                });
            }

            // ---- 游戏主线：七条链全部接上 → 时光机启动（未来人穿越回过去）----
            List<PuzzleStep> backSteps = new List<PuzzleStep>();
            for (int i = 0; i < Chains.Length; i++)
            {
                ChainDef chain = Chains[i];
                backSteps.Add(new PuzzleStep
                {
                    id = "chain_" + chain.Id,
                    title = chain.Title,
                    conditions = Conditions(FlagOn(PuzzleChain.Flag(chain.Id, chain.Stages.Length))),
                });
            }

            puzzles.Add(new PuzzleDefinition
            {
                id = "P_main_time_machine",
                title = "主线：七条链全部接上 → 时光机启动",
                era = EraId.Information,
                mode = PuzzleMode.Steps,
                stepsInOrder = false,
                steps = backSteps,
                onSolved = Effects(new FeedbackEffect
                {
                    message = "（游戏主线）七条链全部接上：未来人发明了时光机，穿越回石器时代。",
                }),
                isMainPuzzle = true,   // 信息时代的主线就是这个（策划图上的「游戏主线」）
            });

            return puzzles;
        }

        /// <summary>这个时代"该接上的环"——每条链在这个时代的那一环（没有就没有）。</summary>
        private static List<PuzzleCondition> EraProgressFlags(EraId era)
        {
            List<PuzzleCondition> conditions = new List<PuzzleCondition>();

            for (int i = 0; i < Chains.Length; i++)
            {
                ChainDef chain = Chains[i];

                for (int s = 0; s < chain.Stages.Length; s++)
                {
                    if (chain.Stages[s].Era == era)
                    {
                        conditions.Add(FlagOn(PuzzleChain.Flag(chain.Id, s + 1)));
                        break;
                    }
                }
            }

            return conditions;
        }

        private static string EraTitle(EraId era) => EraCatalog.Get(era).title;

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
            List<StateGroup> groups, List<VisualStateRule> rules, Transform window, List<SpriteRenderer> shapes, Sprite white,
            string defaultFeedback = "", string category = "", string chain = "", int stage = 0)
        {
            // 高亮框挂在窗口上而不是物体上：这样它不会算进物体的点击区域
            SpriteRenderer halo = CreateHalo(window, shapes, white);

            SerializedObject so = new SerializedObject(target);
            SetString(so, "id", id);
            SetInt(so, "era", (int)era);
            SetString(so, "displayName", displayName);
            SetString(so, "defaultState", defaultState);

            // 「空手点它、又没有任何规则命中时说什么」—— 挂在物体上，不占规则行
            SetString(so, "defaultFeedback", defaultFeedback);

            // 类别：规则表里写 @类别 就能一次管一批（@rift 一条管四个裂隙、@chain 一条管所有链节点）
            SetString(so, "category", category);

            // 链：它是哪条链的第几环 —— 「上一环亮了才点得动」和「点亮我这一环」全靠这两个字段
            SetString(so, "chain", chain);
            SetInt(so, "stage", stage);

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

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.floatValue = value;
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
