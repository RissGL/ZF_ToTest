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
        private const string HaloName = "HoverHalo";

        // 层级：内容 0 / 地景 5 / 时代序号点 7 / 边框 10 / 锁-对勾 20（EraWindowSceneBuilder 定的）
        private const int HaloOrder = PuzzleSortingOrder.Halo;

        /// <summary>
        /// ★ 同一个物体里**重叠的形状必须给递增的层级**（layer 参数）。
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

            EraWindow stone = FindWindow(EraId.Stone);
            EraWindow information = FindWindow(EraId.Information);
            if (stone == null || information == null)
            {
                Debug.LogError("[谜题] 找不到石器时代 / 信息时代的窗口。先重搭一次四个时代窗口。");
                return;
            }

            Sprite white = EraWindowSceneBuilder.EnsureWhiteSprite();
            if (white == null)
            {
                Debug.LogError("[谜题] 占位方块图拿不到，搭不下去。");
                return;
            }

            ClearDemoObjects();

            PuzzleTableSO table = EnsureTable(false);

            BuildRock(stone.transform, white);
            BuildWoodpile(stone.transform, white);
            BuildHearth(information.transform, white);
            BuildRoot(table);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[谜题] 解密演示搭好了。按 Play，然后：\n" +
                      "  1. 点石器时代窗口进去 → 点左上角那块**岩壁** → 拿到「燧石」（Console 会说）\n" +
                      "  2. 按 Tab 把燧石拿在手里 → 点右边的**柴堆** → 火着了，石器时代窗口亮对勾\n" +
                      "  3. ESC 回全景 → 进信息时代 → **壁炉自己烧起来了**（石器时代点的火）\n" +
                      $"  规则表在 {TablePath}，改规则不用改代码。");
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

        // ===================== 规则表 =====================

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

            return new List<InteractionRule>
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
                    if (child.name.StartsWith(InteractablePrefix) || child.name == HaloName)
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

        private static SpriteRenderer CreateHalo(Transform window, List<SpriteRenderer> shapes, Sprite white)
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

            Vector3 center = window.InverseTransformPoint(bounds.center);
            Vector3 size = window.InverseTransformVector(bounds.size);

            SpriteRenderer halo = EraWindowSceneBuilder.CreateRect(window, HaloName,
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
