using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 规则表编辑器：把「一张张展开的 SerializedProperty」变成**一张能读的表**。
    ///
    /// 它不替代 Inspector，而是补上 Inspector 看不到的三件事：
    ///   ① 顺序 = 优先级，谁遮蔽了谁一眼可见（兜底规则放错位置是这个系统最常见的坑）
    ///   ② 引用的 id / flag / 道具到底存不存在（打错字不会报错，只会"点了没反应"）
    ///   ③ 试跑：摆好当前状态，直接看"点它会命中哪条规则、每条为什么没中"，不用按 Play
    ///
    /// 菜单：Tools → 谜题 → 规则表编辑器
    /// </summary>
    public class PuzzleTableEditorWindow : EditorWindow
    {
        private const string LastTableKey = "ZF.Puzzle.LastTable";

        private PuzzleTableSO m_Table;
        private SerializedObject m_Serialized;
        private ReorderableList m_RuleList;
        private PuzzleScanResult m_Scan;

        private Vector2 m_Scroll;
        private int m_SelectedRule = -1;
        private bool m_ShowRuleDetail = true;
        private bool m_ShowSimulator = true;
        private bool m_ShowIssues = true;

        // ---- 试跑面板的状态 ----
        private readonly PuzzleSimulation m_Simulation = new PuzzleSimulation();
        private readonly Dictionary<string, float> m_SimFlags = new Dictionary<string, float>();
        private readonly Dictionary<string, EraId> m_SimCharacters = new Dictionary<string, EraId>();
        private readonly HashSet<string> m_SimItems = new HashSet<string>();
        private string m_SimTarget = "";
        private int m_SimVerb;                  // 0 = 点，1 = 用道具
        private string m_SimItem = "";
        private string m_SimSelectedItem = "";
        private string m_SimSelectedCharacter = "";
        private int m_SimFocusedEra = -1;        // -1 = 全景
        private List<string> m_Trace = new List<string>();

        [MenuItem("Tools/谜题/规则表编辑器", false, 30)]
        public static void Open()
        {
            PuzzleTableEditorWindow window = GetWindow<PuzzleTableEditorWindow>("谜题规则表");
            window.minSize = new Vector2(720f, 560f);

            // 打开时自动抓一张表：优先上次用的，其次工程里第一张
            if (window.m_Table == null)
            {
                string path = EditorPrefs.GetString(LastTableKey, "");
                PuzzleTableSO table = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(path);

                if (table == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:PuzzleTableSO");
                    if (guids.Length > 0)
                    {
                        table = AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }

                window.SetTable(table);
            }

            window.Show();
        }

        private void OnEnable()
        {
            if (m_Table != null)
            {
                Rebuild();
            }
        }

        private void OnGUI()
        {
            if (m_Table == null)
            {
                DrawPicker();
                return;
            }

            if (m_Serialized == null || m_Serialized.targetObject != m_Table)
            {
                Rebuild();
            }

            m_Serialized.Update();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawRules();
            EditorGUILayout.Space(6f);
            DrawRuleDetail();
            EditorGUILayout.Space(6f);
            DrawSimulator();
            EditorGUILayout.Space(6f);
            DrawIssues();

            EditorGUILayout.EndScrollView();

            m_Serialized.ApplyModifiedProperties();
        }

        // ===================== 顶部 =====================

        private void DrawPicker()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "先选一张谜题规则表。\n" +
                "菜单 Tools → 谜题 → 搭建解密演示 会自动生成一张（PuzzleTable_Demo.asset）。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            PuzzleTableSO picked = (PuzzleTableSO)EditorGUILayout.ObjectField("规则表", null, typeof(PuzzleTableSO), false);
            if (EditorGUI.EndChangeCheck() && picked != null)
            {
                SetTable(picked);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                PuzzleTableSO picked = (PuzzleTableSO)EditorGUILayout.ObjectField(
                    m_Table, typeof(PuzzleTableSO), false, GUILayout.Width(240f));
                if (EditorGUI.EndChangeCheck())
                {
                    SetTable(picked);
                    GUIUtility.ExitGUI();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("重新扫描", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    Rescan();
                }

                string summary = m_Scan == null
                    ? ""
                    : $"{m_Table.interactionRules.Count} 条规则 · {m_Table.puzzles.Count} 个谜题 · " +
                      $"{(m_Scan.Errors > 0 ? "❌ " + m_Scan.Errors + " 个错误" : "✅ 无错误")}" +
                      (m_Scan.Warnings > 0 ? $" · ⚠ {m_Scan.Warnings} 个提醒" : "");

                GUILayout.Label(summary, EditorStyles.miniLabel);
            }
        }

        // ===================== 规则列表 =====================

        private void DrawRules()
        {
            m_RuleList.DoLayoutList();
        }

        private void DrawRuleRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            InteractionRule rule = GetRule(index);
            if (rule == null)
            {
                EditorGUI.LabelField(rect, "(空)");
                return;
            }

            List<PuzzleIssue> issues = m_Scan != null ? m_Scan.IssuesOf(index) : null;
            bool hasError = false;
            bool hasWarning = false;

            if (issues != null)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    hasError |= issues[i].IsError;
                    hasWarning |= !issues[i].IsError;
                }
            }

            Rect line1 = new Rect(rect.x, rect.y + 1f, rect.width, 15f);
            Rect line2 = new Rect(rect.x, rect.y + 16f, rect.width, 15f);

            string mark = hasError ? "❌ " : hasWarning ? "⚠ " : "";
            string target = string.IsNullOrEmpty(rule.targetId) ? "*任意物体*" : rule.targetId;
            string verb = VerbText(rule.verb) + (string.IsNullOrEmpty(rule.itemId) ? "" : $" {rule.itemId}");

            EditorGUI.LabelField(line1, $"{mark}#{index + 1}  {target}  ·  {verb}" +
                                        (string.IsNullOrEmpty(rule.note) ? "" : $"   — {rule.note}"),
                hasError ? EditorStyles.boldLabel : EditorStyles.label);

            string conditions = ConditionsText(rule.conditions);
            string effects = EffectsText(rule.effects);
            EditorGUI.LabelField(line2,
                $"      条件：{conditions}   →   {effects}" +
                (string.IsNullOrEmpty(rule.elseFeedback) ? "" : $"   （不满足时：「{rule.elseFeedback}」）"),
                EditorStyles.miniLabel);
        }

        private void DrawRuleDetail()
        {
            m_ShowRuleDetail = EditorGUILayout.Foldout(m_ShowRuleDetail, "选中规则的完整字段", true);
            if (!m_ShowRuleDetail)
            {
                return;
            }

            if (m_SelectedRule < 0 || m_SelectedRule >= m_RuleList.count)
            {
                EditorGUILayout.HelpBox("在上面点一行看它的完整字段。", MessageType.None);
                return;
            }

            SerializedProperty element = m_RuleList.serializedProperty.GetArrayElementAtIndex(m_SelectedRule);
            EditorGUILayout.PropertyField(element, GUIContent.none, true);

            List<PuzzleIssue> issues = m_Scan != null ? m_Scan.IssuesOf(m_SelectedRule) : null;
            if (issues != null && issues.Count > 0)
            {
                EditorGUILayout.Space(2f);
                for (int i = 0; i < issues.Count; i++)
                {
                    EditorGUILayout.HelpBox(issues[i].Message, issues[i].IsError ? MessageType.Error : MessageType.Warning);
                }
            }
        }

        // ===================== 试跑 =====================

        private void DrawSimulator()
        {
            m_ShowSimulator = EditorGUILayout.Foldout(m_ShowSimulator, "试跑：不用按 Play，直接看会命中哪条规则", true);
            if (!m_ShowSimulator || m_Scan == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 目标
                List<string> ids = Sorted(m_Scan.InteractionIds);
                if (ids.Count == 0)
                {
                    EditorGUILayout.HelpBox("场景里没有可交互物（先去跑一次「搭建解密演示」）。", MessageType.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(m_SimTarget) || !ids.Contains(m_SimTarget))
                {
                    m_SimTarget = ids[0];
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("点谁", GUILayout.Width(60f));
                    m_SimTarget = Popup("", m_SimTarget, ids);

                    m_SimVerb = EditorGUILayout.Popup(m_SimVerb, new[] { "空手点", "用道具点" }, GUILayout.Width(90f));

                    if (m_SimVerb == 1)
                    {
                        List<string> items = Sorted(m_Scan.GivenItems);
                        items.Insert(0, "(无)");
                        int index = Mathf.Max(0, items.IndexOf(m_SimItem));
                        index = EditorGUILayout.Popup(index, items.ToArray(), GUILayout.Width(120f));
                        m_SimItem = items[index] == "(无)" ? "" : items[index];
                    }
                    else
                    {
                        m_SimItem = "";
                    }
                }

                // 背包里有什么
                EditorGUILayout.LabelField("背包（试跑时假设已经拿到）", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    List<string> items = Sorted(m_Scan.GivenItems);
                    if (items.Count == 0)
                    {
                        GUILayout.Label("（表里没有任何 GiveItemEffect）", EditorStyles.miniLabel);
                    }

                    for (int i = 0; i < items.Count; i++)
                    {
                        bool has = m_SimItems.Contains(items[i]);
                        bool now = GUILayout.Toggle(has, items[i], EditorStyles.miniButton, GUILayout.Width(90f));
                        if (now != has)
                        {
                            if (now)
                            {
                                m_SimItems.Add(items[i]);
                            }
                            else
                            {
                                m_SimItems.Remove(items[i]);
                            }
                        }
                    }
                }

                // 人物位置
                if (m_Scan.CharacterIds.Count > 0)
                {
                    EditorGUILayout.LabelField("人物在哪个时代", EditorStyles.miniBoldLabel);
                    foreach (string character in Sorted(m_Scan.CharacterIds))
                    {
                        if (!m_SimCharacters.ContainsKey(character))
                        {
                            m_SimCharacters[character] = EraOf(character);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(character, GUILayout.Width(80f));
                            m_SimCharacters[character] = (EraId)EditorGUILayout.EnumPopup(m_SimCharacters[character], GUILayout.Width(120f));

                            bool selected = PuzzleOps.SameState(m_SimSelectedCharacter, character);
                            bool now = GUILayout.Toggle(selected, "点名他", EditorStyles.miniButton, GUILayout.Width(70f));
                            if (now && !selected)
                            {
                                m_SimSelectedCharacter = character;
                            }
                            else if (!now && selected)
                            {
                                m_SimSelectedCharacter = "";
                            }
                        }
                    }
                }

                // flag
                List<string> flags = Sorted(Union(m_Scan.WrittenFlags, m_Scan.ReadFlags));
                if (flags.Count > 0)
                {
                    EditorGUILayout.LabelField("flag 值", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < flags.Count; i++)
                    {
                        string flag = flags[i];
                        if (!m_SimFlags.ContainsKey(flag))
                        {
                            m_SimFlags[flag] = 0f;
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(flag, GUILayout.Width(200f));
                            m_SimFlags[flag] = EditorGUILayout.FloatField(m_SimFlags[flag], GUILayout.Width(70f));

                            bool on = m_SimFlags[flag] != 0f;
                            bool now = GUILayout.Toggle(on, "开", EditorStyles.miniButton, GUILayout.Width(40f));
                            if (now != on)
                            {
                                m_SimFlags[flag] = now ? 1f : 0f;
                            }
                        }
                    }
                }

                // 玩家现在进在第几个时代
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("玩家现在进在", GUILayout.Width(80f));
                    m_SimFocusedEra = EditorGUILayout.IntPopup(m_SimFocusedEra,
                        new[] { "全景", "石器时代", "蒸汽时代", "电气时代", "信息时代" },
                        new[] { -1, 0, 1, 2, 3 }, GUILayout.Width(120f));

                    if (GUILayout.Button("跑一次", GUILayout.Width(80f)))
                    {
                        RunSimulation();
                    }
                }

                if (m_Trace.Count > 0)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField("过程", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < m_Trace.Count; i++)
                    {
                        EditorGUILayout.LabelField(m_Trace[i], EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void RunSimulation()
        {
            m_Simulation.FocusedEraIndex = m_SimFocusedEra;
            m_Simulation.Apply(m_SimFlags, m_SimItems, m_SimSelectedItem, m_SimCharacters, m_SimSelectedCharacter);

            Verb verb = m_SimVerb == 1 && !string.IsNullOrEmpty(m_SimItem) ? Verb.UseItem : Verb.Interact;

            // 走的是运行时那套匹配逻辑（PuzzleRuleMatcher），不是另写一套
            m_Trace = m_Simulation.Trace(m_Table, m_SimTarget, verb, verb == Verb.UseItem ? m_SimItem : "");

            if (verb != Verb.UseItem && m_SimVerb == 1)
            {
                m_Trace.Insert(0, "（没选道具，按空手点跑）");
            }
        }

        // ===================== 问题列表 =====================

        private void DrawIssues()
        {
            if (m_Scan == null)
            {
                return;
            }

            m_ShowIssues = EditorGUILayout.Foldout(m_ShowIssues,
                $"校验问题（{m_Scan.Errors} 个错误 / {m_Scan.Warnings} 个提醒）", true);

            if (!m_ShowIssues)
            {
                return;
            }

            if (m_Scan.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("没查出问题。", MessageType.Info);
                return;
            }

            for (int i = 0; i < m_Scan.Issues.Count; i++)
            {
                PuzzleIssue issue = m_Scan.Issues[i];
                string prefix = issue.RuleIndex >= 0 ? $"规则 #{issue.RuleIndex + 1}：" : "";
                EditorGUILayout.HelpBox(prefix + issue.Message, issue.IsError ? MessageType.Error : MessageType.Warning);
            }
        }

        // ===================== 杂项 =====================

        private void SetTable(PuzzleTableSO table)
        {
            m_Table = table;
            m_SelectedRule = -1;
            m_Trace.Clear();
            m_SimFlags.Clear();
            m_SimCharacters.Clear();
            m_SimItems.Clear();
            m_SimSelectedItem = "";
            m_SimSelectedCharacter = "";

            Rebuild();

            if (m_Table != null)
            {
                EditorPrefs.SetString(LastTableKey, AssetDatabase.GetAssetPath(m_Table));
            }
        }

        private void Rebuild()
        {
            m_Serialized = m_Table != null ? new SerializedObject(m_Table) : null;

            if (m_Serialized == null)
            {
                m_RuleList = null;
                m_Scan = null;
                return;
            }

            m_RuleList = new ReorderableList(m_Serialized, m_Serialized.FindProperty("interactionRules"),
                true, true, true, true)
            {
                elementHeight = 34f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect,
                    "交互规则 —— 从上往下 = 优先级，第一条「目标+动作匹配 且 条件全满足」的生效（特殊条件在前，兜底在后）"),
                drawElementCallback = DrawRuleRow,
                onSelectCallback = list =>
                {
                    m_SelectedRule = list.index;
                    Repaint();
                },
                onChangedCallback = list =>
                {
                    m_Serialized.ApplyModifiedProperties();
                    Rescan();
                },
            };

            Rescan();
        }

        private void Rescan()
        {
            m_Scan = PuzzleEditorScan.Scan(m_Table);
            Repaint();
        }

        private InteractionRule GetRule(int index)
        {
            if (m_Table == null || m_Table.interactionRules == null ||
                index < 0 || index >= m_Table.interactionRules.Count)
            {
                return null;
            }

            return m_Table.interactionRules[index];
        }

        private EraId EraOf(string characterId)
        {
            CharacterView[] views = Object.FindObjectsOfType<CharacterView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && PuzzleOps.SameState(views[i].CharacterId, characterId))
                {
                    return views[i].HomeEra;
                }
            }

            return EraId.Stone;
        }

        private static string VerbText(Verb verb)
        {
            switch (verb)
            {
                case Verb.Interact: return "空手点";
                case Verb.UseItem: return "用道具";
                default: return "任意动作";
            }
        }

        private static string ConditionsText(List<PuzzleCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return "无条件（会挡住下面同类的规则！）";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < conditions.Count; i++)
            {
                parts.Add(conditions[i] != null ? conditions[i].Describe() : "(空)");
            }

            return string.Join(" 且 ", parts);
        }

        private static string EffectsText(List<PuzzleEffect> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return "(没有效果，只发事件)";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                parts.Add(effects[i] != null ? effects[i].Describe() : "(空)");
            }

            return string.Join(" → ", parts);
        }

        private static List<string> Sorted(IEnumerable<string> source)
        {
            List<string> list = new List<string>();
            foreach (string value in source)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    list.Add(value);
                }
            }

            list.Sort();
            return list;
        }

        private static IEnumerable<string> Union(HashSet<string> a, HashSet<string> b)
        {
            HashSet<string> union = new HashSet<string>(a);
            union.UnionWith(b);
            return union;
        }

        private static string Popup(string label, string current, List<string> options)
        {
            int index = Mathf.Max(0, options.IndexOf(current));
            index = EditorGUILayout.Popup(index, options.ToArray(), GUILayout.Width(140f));
            return options[index];
        }
    }
}
