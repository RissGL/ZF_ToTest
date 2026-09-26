using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 谜题图编辑器：**左边是关系图，右边是原生字段面板**。
    ///
    /// 一个窗口干完原来的两件事：
    ///   · 看关系（谁写了 flag、谁读它、跨时代那条链、死锁）
    ///   · 改规则（选中规则节点 → 右侧直接编辑；上移/下移就是改优先级）
    ///
    /// 菜单：Tools → 谜题 → 谜题图
    /// </summary>
    public class PuzzleGraphWindow : EditorWindow
    {
        private const string LastTableKey = "ZF.Puzzle.LastTable";
        private const float PanelWidth = 430f;

        private static readonly Dictionary<string, Rect> s_Layout = new Dictionary<string, Rect>();

        private PuzzleGraphView m_Graph;
        private PuzzleTableSO m_Table;
        private SerializedObject m_Serialized;
        private PuzzleScanResult m_Scan;

        private Label m_Summary;
        private Label m_PanelTitle;
        private IMGUIContainer m_PanelInspector;
        private VisualElement m_PanelButtons;
        private VisualElement m_PanelRelated;
        private VisualElement m_PanelTrace;
        private VisualElement m_PanelIssues;

        private int m_SelectedRule = -1;
        private string m_SelectedKey = "";
        private string m_SelectedPuzzleId = "";
        private string m_LastSignature = "";
        private bool m_GraphDirty;
        private bool m_UndoDirty;
        private int m_PendingMove;
        private int m_PendingDeleteRule = -1;
        private int m_PendingDeletePuzzle = -1;
        private PuzzleForm m_Form;

        private bool m_ShowObjects = true;
        private bool m_ShowRules = true;
        private bool m_ShowFlags = true;
        private bool m_ShowItems = true;
        private bool m_ShowPuzzles = true;
        private bool m_ShowEras = true;

        [MenuItem("Tools/谜题/谜题图", false, 30)]
        public static void Open()
        {
            PuzzleGraphWindow window = GetWindow<PuzzleGraphWindow>("谜题图");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            // ---------- 左：图 ----------
            VisualElement left = new VisualElement();
            left.style.flexGrow = 1f;
            left.style.flexDirection = FlexDirection.Column;
            root.Add(left);

            left.Add(BuildToolbar());

            m_Graph = new PuzzleGraphView();
            m_Graph.style.flexGrow = 1f;

            // 拖规则节点上下 = 改优先级（松手时重排）
            m_Graph.RegisterCallback<MouseUpEvent>(OnGraphMouseUp, TrickleDown.TrickleDown);

            // 右键空白处 = 新建
            m_Graph.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("新建谜题", _ => AddPuzzle());
                evt.menu.AppendAction("新建规则（目标留空）", _ => AddRule(""));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("重新扫描", _ => RebuildAll());
            }));

            left.Add(m_Graph);

            // ---------- 右：面板 ----------
            VisualElement panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            panel.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            root.Add(panel);

            m_PanelTitle = new Label("点一个节点或规则");
            m_PanelTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_PanelTitle.style.whiteSpace = WhiteSpace.Normal;
            m_PanelTitle.style.paddingLeft = 8f;
            m_PanelTitle.style.paddingTop = 8f;
            m_PanelTitle.style.paddingRight = 8f;
            panel.Add(m_PanelTitle);

            ScrollView scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            panel.Add(scroll);

            m_PanelInspector = new IMGUIContainer();
            m_PanelInspector.style.display = DisplayStyle.None;
            scroll.Add(m_PanelInspector);

            m_PanelButtons = new VisualElement();
            m_PanelButtons.style.flexDirection = FlexDirection.Row;
            m_PanelButtons.style.flexWrap = Wrap.Wrap;
            m_PanelButtons.style.display = DisplayStyle.None;
            scroll.Add(m_PanelButtons);

            m_PanelRelated = new VisualElement();
            scroll.Add(m_PanelRelated);

            m_PanelTrace = new VisualElement();
            scroll.Add(m_PanelTrace);

            m_PanelIssues = new VisualElement();
            scroll.Add(m_PanelIssues);

            m_Table = ResolveTable();
            RebuildAll();
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 6f;
            toolbar.style.paddingRight = 6f;
            toolbar.style.paddingTop = 4f;
            toolbar.style.paddingBottom = 4f;

            ObjectField tableField = new ObjectField("规则表")
            {
                objectType = typeof(PuzzleTableSO),
                allowSceneObjects = false,
                value = m_Table,
                style = { width = 300f },
            };
            tableField.RegisterValueChangedCallback(evt =>
            {
                m_Table = evt.newValue as PuzzleTableSO;
                if (m_Table != null)
                {
                    EditorPrefs.SetString(LastTableKey, AssetDatabase.GetAssetPath(m_Table));
                }

                s_Layout.Clear();
                RebuildAll();
            });
            toolbar.Add(tableField);

            toolbar.Add(Button("↶ 撤回", DoUndo));
            toolbar.Add(Button("↷ 重做", DoRedo));
            toolbar.Add(Button("重新扫描", RebuildAll));
            toolbar.Add(Button("＋ 新建规则", () => AddRule("")));
            toolbar.Add(Button("＋ 新建谜题", AddPuzzle));
            toolbar.Add(Button("重新布局", () =>
            {
                s_Layout.Clear();
                RebuildGraph();
            }));
            toolbar.Add(Button("居中", () => m_Graph?.FrameAll()));

            toolbar.Add(new Label("  显示") { style = { marginLeft = 6f, fontSize = 10f } });
            toolbar.Add(Toggle("物体", m_ShowObjects, v => { m_ShowObjects = v; RebuildGraph(); }));
            toolbar.Add(Toggle("规则", m_ShowRules, v => { m_ShowRules = v; RebuildGraph(); }));
            toolbar.Add(Toggle("flag", m_ShowFlags, v => { m_ShowFlags = v; RebuildGraph(); }));
            toolbar.Add(Toggle("道具", m_ShowItems, v => { m_ShowItems = v; RebuildGraph(); }));
            toolbar.Add(Toggle("谜题", m_ShowPuzzles, v => { m_ShowPuzzles = v; RebuildGraph(); }));
            toolbar.Add(Toggle("时代", m_ShowEras, v => { m_ShowEras = v; RebuildGraph(); }));

            m_Summary = new Label();
            m_Summary.style.marginLeft = 8f;
            m_Summary.style.fontSize = 10f;
            toolbar.Add(m_Summary);

            return toolbar;
        }

        private static Button Button(string text, System.Action onClick)
        {
            Button button = new Button(onClick) { text = text };
            button.style.marginLeft = 4f;
            button.style.fontSize = 11f;
            return button;
        }

        private static Toggle Toggle(string text, bool value, System.Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(text) { value = value };
            toggle.style.marginLeft = 2f;
            toggle.style.fontSize = 10f;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }

        private static PuzzleTableSO ResolveTable()
        {
            string path = EditorPrefs.GetString(LastTableKey, "");
            PuzzleTableSO table = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(path);

            if (table == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:PuzzleTableSO");
                if (guids.Length > 0)
                {
                    table = AssetDatabase.LoadAssetAtPath<PuzzleTableSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            return table;
        }

        private void Update()
        {
            if (m_Graph == null)
            {
                return;
            }

            string signature = SelectionSignature();
            if (signature != m_LastSignature)
            {
                m_LastSignature = signature;
                RefreshPanel();
            }

            if (m_GraphDirty)
            {
                m_GraphDirty = false;
                RebuildGraph();
            }

            if (m_UndoDirty)
            {
                m_UndoDirty = false;
                RebuildAll();
            }

            // 删除也推迟到下一帧：面板上的按钮回调里重建面板 = 把它自己拆掉，容易出怪事
            if (m_PendingDeleteRule >= 0)
            {
                int index = m_PendingDeleteRule;
                m_PendingDeleteRule = -1;
                DeleteRule(index);
            }

            if (m_PendingDeletePuzzle >= 0)
            {
                int index = m_PendingDeletePuzzle;
                m_PendingDeletePuzzle = -1;
                DeletePuzzle(index);
            }

            // 改优先级这类"动了数组"的操作都推迟到这里做 —— 别在 IMGUI 画到一半时把数组换掉
            if (m_PendingMove != 0)
            {
                int offset = m_PendingMove;
                m_PendingMove = 0;

                if (m_SelectedRule >= 0)
                {
                    MoveRule(m_SelectedRule, m_SelectedRule + offset);
                }
            }
        }

        /// <summary>松手时按 y 位置给规则节点重排优先级（拖上拖下就是改顺序）。</summary>
        private void OnGraphMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0 || m_Table == null || m_Graph == null || m_Graph.RuleNodes.Count < 2)
            {
                return;
            }

            List<KeyValuePair<int, float>> order = new List<KeyValuePair<int, float>>();
            foreach (KeyValuePair<int, PuzzleGraphNode> pair in m_Graph.RuleNodes)
            {
                order.Add(new KeyValuePair<int, float>(pair.Key, pair.Value.GetPosition().y));
            }

            order.Sort((a, b) => a.Value.CompareTo(b.Value));

            bool changed = false;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Key != i)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            ApplyRuleOrder(order);
        }

        /// <summary>把规则数组按给定的原索引顺序重排（用 MoveArrayElement 做插入排序）。</summary>
        private void ApplyRuleOrder(List<KeyValuePair<int, float>> order)
        {
            if (m_Serialized == null || m_Table == null || order.Count != m_Table.interactionRules.Count)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");

            List<int> current = new List<int>();
            for (int i = 0; i < order.Count; i++)
            {
                current.Add(i);
            }

            for (int i = 0; i < order.Count; i++)
            {
                int want = order[i].Key;
                int at = current.IndexOf(want);

                if (at != i && at >= 0)
                {
                    rules.MoveArrayElement(at, i);
                    current.RemoveAt(at);
                    current.Insert(i, want);
                }
            }

            m_Serialized.ApplyModifiedProperties();
            RebuildAll();
        }

        // ===================== 扫描 / 重建 =====================

        // 关于 Undo：这里**故意不写** Undo.RecordObject。
        // SerializedObject.ApplyModifiedProperties() 本身就会登记 Undo（不然 Unity 不会另给一个
        // ApplyModifiedPropertiesWithoutUndo 当"不要 Undo"的选项）。
        // 反过来，在 Update() 和 Apply() 之间插一个 Undo.RecordObject，会让 SerializedObject
        // 重新读对象、把手里那个 SerializedProperty 弄失效 —— 紧接着的增删就静默失效了（踩过：删不掉）。

        private static void Log(string message) => Debug.Log($"[谜题图] {message}");

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedoPerformed;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedoPerformed;

        /// <summary>别处按 Ctrl+Z（比如在 Inspector 里改的）也要让图跟着刷新。</summary>
        private void OnUndoRedoPerformed()
        {
            m_UndoDirty = true;
            Log("撤回 / 重做了一次改动。");
        }

        private void DoUndo() => Undo.PerformUndo();

        private void DoRedo() => Undo.PerformRedo();

        private void RebuildAll()
        {
            if (m_Graph == null)
            {
                return;
            }

            m_Serialized = m_Table != null ? new SerializedObject(m_Table) : null;
            m_Scan = PuzzleEditorScan.Scan(m_Table);
            m_Form = null;
            m_SelectedRule = -1;
            m_SelectedKey = "";
            m_LastSignature = "";
            RebuildGraph();
            RefreshPanel();
        }

        private void RebuildGraph()
        {
            if (m_Graph == null)
            {
                return;
            }

            PuzzleGraphBuilder.Build(m_Graph, m_Table, m_Scan, s_Layout,
                m_ShowObjects, m_ShowRules, m_ShowFlags, m_ShowItems, m_ShowPuzzles, m_ShowEras);

            if (m_Summary != null)
            {
                int nodes = m_Graph.Nodes.Count;
                m_Summary.text = m_Table == null
                    ? "没有规则表"
                    : $"{m_Table.interactionRules.Count} 条规则 · {nodes} 个节点 · " +
                      (m_Scan.Errors > 0 ? $"❌ {m_Scan.Errors}" : "✅ 无错误") +
                      (m_Scan.Warnings > 0 ? $" · ⚠ {m_Scan.Warnings}" : "");
            }
        }

        // ===================== 选中 → 面板 =====================

        private string SelectionSignature()
        {
            if (m_Graph == null)
            {
                return "";
            }

            foreach (ISelectable selectable in m_Graph.selection)
            {
                if (selectable is PuzzleGraphNode node)
                {
                    return node.RuleIndex >= 0 ? "rule:" + node.RuleIndex : "key:" + node.Key;
                }
            }

            return "";
        }

        private void RefreshPanel()
        {
            m_SelectedRule = -1;
            m_SelectedKey = "";

            if (m_Graph != null)
            {
                foreach (ISelectable selectable in m_Graph.selection)
                {
                    if (selectable is PuzzleGraphNode node)
                    {
                        m_SelectedRule = node.RuleIndex;
                        m_SelectedKey = node.Key;
                        break;
                    }
                }
            }

            m_PanelRelated.Clear();
            m_PanelTrace.Clear();

            if (m_SelectedRule >= 0)
            {
                ShowRulePanel(m_SelectedRule);
            }
            else if (m_SelectedKey.StartsWith("puzzle:"))
            {
                ShowPuzzlePanel(m_SelectedKey.Substring(7));
            }
            else if (!string.IsNullOrEmpty(m_SelectedKey))
            {
                ShowStatePanel(m_SelectedKey);
            }
            else
            {
                m_PanelTitle.text = "点一个节点或规则";
                m_PanelInspector.style.display = DisplayStyle.None;
                m_PanelButtons.style.display = DisplayStyle.None;
            }

            ShowIssues(m_SelectedRule);
        }

        private void ShowRulePanel(int index)
        {
            InteractionRule rule = GetRule(index);

            m_PanelTitle.text = rule == null
                ? $"规则 #{index + 1}（空）"
                : $"规则 #{index + 1}   {rule.targetId} · {VerbText(rule.verb)}" +
                  (string.IsNullOrEmpty(rule.note) ? "" : $"\n{rule.note}");

            m_PanelInspector.style.display = DisplayStyle.Flex;
            m_PanelInspector.onGUIHandler = () => DrawRuleInspector(index);

            m_PanelButtons.style.display = DisplayStyle.Flex;
            m_PanelButtons.Clear();

            m_PanelButtons.Add(Button("复制这条", () => DuplicateRule(index)));
            m_PanelButtons.Add(Button("删除这条", () => m_PendingDeleteRule = index));
            m_PanelButtons.Add(Button("选中它的目标", () => SelectTarget(rule != null ? rule.targetId : "")));
        }

        // ===================== 谜题 =====================

        private void ShowPuzzlePanel(string puzzleId)
        {
            int index = PuzzleIndexOf(puzzleId);
            PuzzleDefinition puzzle = index >= 0 ? m_Table.puzzles[index] : null;

            m_SelectedPuzzleId = puzzleId;

            m_PanelTitle.text = puzzle == null
                ? "谜题（找不到）"
                : $"谜题：{(string.IsNullOrEmpty(puzzle.title) ? puzzle.id : puzzle.title)}" +
                  (puzzle.isMainPuzzle ? "   ★ 主线（解开 = 这个时代通关）" : "");

            m_PanelInspector.style.display = DisplayStyle.Flex;
            m_PanelInspector.onGUIHandler = () => DrawPuzzleInspector(index);

            m_PanelButtons.style.display = DisplayStyle.Flex;
            m_PanelButtons.Clear();
            m_PanelButtons.Add(Button("复制这个谜题", () => DuplicatePuzzle(index)));
            m_PanelButtons.Add(Button("删除这个谜题", () => m_PendingDeletePuzzle = index));
            m_PanelButtons.Add(Button("改完 id 后重新扫描", RebuildAll));
        }

        private void DrawPuzzleInspector(int index)
        {
            if (m_Serialized == null)
            {
                return;
            }

            if (m_Form == null)
            {
                m_Form = new PuzzleForm(m_Scan);
            }

            m_Form.SetPuzzleIds(PuzzleIds());

            m_Serialized.Update();
            SerializedProperty puzzles = m_Serialized.FindProperty("puzzles");

            if (puzzles == null || index < 0 || index >= puzzles.arraySize)
            {
                return;
            }

            if (m_Form.DrawPuzzle(puzzles.GetArrayElementAtIndex(index), index))
            {
                m_Serialized.ApplyModifiedProperties();

                // 只改节点文字，不整张图重建（不然打字时选中会丢、输入框会失焦）
                RefreshPuzzleNode();

                m_Scan = PuzzleEditorScan.Scan(m_Table);
                m_Form.RefreshCandidates();
                ShowIssues(-1);
            }
        }

        /// <summary>改谜题时只更新那个节点的文字。改了 id 会导致找不到节点 —— 那就点一下「重新扫描」。</summary>
        private void RefreshPuzzleNode()
        {
            if (m_Graph == null || m_Table == null || string.IsNullOrEmpty(m_SelectedPuzzleId))
            {
                return;
            }

            PuzzleGraphNode node = m_Graph.Get("puzzle:" + m_SelectedPuzzleId);
            if (node == null)
            {
                return;
            }

            int index = PuzzleIndexOf(m_SelectedPuzzleId);
            PuzzleDefinition puzzle = index >= 0 ? m_Table.puzzles[index] : null;
            if (puzzle == null)
            {
                return;
            }

            node.title = string.IsNullOrEmpty(puzzle.title) ? puzzle.id : puzzle.title;
            node.SetBody((puzzle.isMainPuzzle ? "主线：解开 = 这个时代通关" : "支线") + $"\n条件 {puzzle.conditions.Count} 条");
        }

        private int PuzzleIndexOf(string puzzleId)
        {
            if (m_Table == null || string.IsNullOrEmpty(puzzleId))
            {
                return -1;
            }

            for (int i = 0; i < m_Table.puzzles.Count; i++)
            {
                if (m_Table.puzzles[i] != null && PuzzleOps.SameState(m_Table.puzzles[i].id, puzzleId))
                {
                    return i;
                }
            }

            return -1;
        }

        private void AddPuzzle()
        {
            if (m_Serialized == null)
            {
                return;
            }

            string id = UniquePuzzleId("P_new");

            m_Serialized.Update();
            SerializedProperty puzzles = m_Serialized.FindProperty("puzzles");
            int index = puzzles.arraySize;
            puzzles.InsertArrayElementAtIndex(index);

            SerializedProperty added = puzzles.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("id").stringValue = id;
            added.FindPropertyRelative("title").stringValue = "新谜题";
            added.FindPropertyRelative("era").intValue = 0;
            added.FindPropertyRelative("isMainPuzzle").boolValue = false;
            added.FindPropertyRelative("conditions").arraySize = 0;
            added.FindPropertyRelative("onSolved").arraySize = 0;

            m_Serialized.ApplyModifiedProperties();
            Log($"已新建谜题「{id}」—— 右边填完成条件；想让它代表「这个时代通关」就勾上主线。");

            RebuildAll();
            FocusPuzzle(id);
        }

        /// <summary>拿选中的那个状态节点当第一条完成条件，直接建一个谜题（图上最顺手的写法）。</summary>
        private void AddPuzzleWithCondition(string key)
        {
            if (m_Serialized == null)
            {
                return;
            }

            string id = UniquePuzzleId("P_new");

            m_Serialized.Update();
            SerializedProperty puzzles = m_Serialized.FindProperty("puzzles");
            int index = puzzles.arraySize;
            puzzles.InsertArrayElementAtIndex(index);

            SerializedProperty added = puzzles.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("id").stringValue = id;
            added.FindPropertyRelative("title").stringValue = "新谜题";
            added.FindPropertyRelative("era").intValue = 0;
            added.FindPropertyRelative("isMainPuzzle").boolValue = false;

            SerializedProperty conditions = added.FindPropertyRelative("conditions");
            PuzzleCondition condition = ConditionForKey(key);

            conditions.arraySize = condition != null ? 1 : 0;
            if (condition != null)
            {
                conditions.GetArrayElementAtIndex(0).managedReferenceValue = condition;
            }

            added.FindPropertyRelative("onSolved").arraySize = 0;
            m_Serialized.ApplyModifiedProperties();
            Log($"已新建谜题「{id}」，完成条件自动填成了「{(condition != null ? condition.Describe() : "空")}」。");

            RebuildAll();
            FocusPuzzle(id);
        }

        /// <summary>自动 id 保证不重名（P_new / P_new_2 / …）。</summary>
        private string UniquePuzzleId(string prefix)
        {
            if (m_Table == null || PuzzleIndexOf(prefix) < 0)
            {
                return prefix;
            }

            for (int i = 2; i < 999; i++)
            {
                string candidate = $"{prefix}_{i}";
                if (PuzzleIndexOf(candidate) < 0)
                {
                    return candidate;
                }
            }

            return prefix + "_x";
        }

        private static PuzzleCondition ConditionForKey(string key)
        {
            if (key.StartsWith("flag:"))
            {
                return new FlagCondition { flag = key.Substring(5), op = FlagOp.Equals, value = 1f };
            }

            if (key.StartsWith("item:"))
            {
                return new HasItemCondition { itemId = key.Substring(5) };
            }

            if (key.StartsWith("obj:"))
            {
                return new ObjectStateCondition { interactableId = key.Substring(4), state = "lit" };
            }

            return null;
        }

        private void DuplicatePuzzle(int index)
        {
            if (m_Serialized == null || m_Table == null || index < 0 || index >= m_Table.puzzles.Count)
            {
                return;
            }

            string id = UniquePuzzleId("P_copy");

            m_Serialized.Update();
            SerializedProperty puzzles = m_Serialized.FindProperty("puzzles");
            puzzles.InsertArrayElementAtIndex(index);
            puzzles.GetArrayElementAtIndex(index + 1).FindPropertyRelative("id").stringValue = id;
            m_Serialized.ApplyModifiedProperties();
            Log($"已复制成「{id}」。");

            RebuildAll();
            FocusPuzzle(id);
        }

        private void DeletePuzzle(int index)
        {
            if (m_Serialized == null)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty puzzles = m_Serialized.FindProperty("puzzles");

            if (index < 0 || index >= puzzles.arraySize)
            {
                return;
            }

            string id = puzzles.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue;

            puzzles.DeleteArrayElementAtIndex(index);
            m_Serialized.ApplyModifiedProperties();

            RebuildAll();
            Log($"已删除谜题「{id}」—— Ctrl+Z（或工具栏的 ↶ 撤回）可以撤销");
        }

        private void FocusPuzzle(string puzzleId)
        {
            PuzzleGraphNode node = m_Graph != null ? m_Graph.Get("puzzle:" + puzzleId) : null;
            if (node == null)
            {
                return;
            }

            m_Graph.ClearSelection();
            m_Graph.AddToSelection(node);
            m_Graph.FrameSelection();
            m_LastSignature = "";
            RefreshPanel();
        }

        private void ShowStatePanel(string key)
        {            m_PanelInspector.style.display = DisplayStyle.None;
            m_PanelButtons.style.display = DisplayStyle.None;
            m_PanelTitle.text = key;

            // 和这个状态有关的规则，点一下就跳过去
            Label hint = new Label("和它有关的规则（点一条选中它）");
            hint.style.fontSize = 10f;
            hint.style.paddingLeft = 8f;
            hint.style.paddingTop = 4f;
            m_PanelRelated.Add(hint);

            if (m_Table == null)
            {
                return;
            }

            for (int i = 0; i < m_Table.interactionRules.Count; i++)
            {
                InteractionRule rule = m_Table.interactionRules[i];
                if (rule == null || !Touches(rule, key))
                {
                    continue;
                }

                int captured = i;
                Button button = new Button(() => FocusRule(captured))
                {
                    text = $"#{i + 1}  {(string.IsNullOrEmpty(rule.note) ? rule.targetId : rule.note)}",
                };
                button.style.marginLeft = 8f;
                button.style.marginRight = 8f;
                button.style.fontSize = 10f;
                m_PanelRelated.Add(button);
            }

            // 选中物体 / 人物时给个试跑 + 直接加一条针对它的规则
            if (key.StartsWith("obj:"))
            {
                string targetId = key.Substring(4);
                m_PanelTrace.Add(Button("新建一条针对它的规则", () => AddRule(targetId)));
                m_PanelTrace.Add(Button("试跑：初始状态点它", () => RunTrace(targetId, false)));
                m_PanelTrace.Add(Button("试跑：条件全满足点它", () => RunTrace(targetId, true)));
            }

            // 拿这个状态当完成条件，直接建一个谜题 —— 图上最顺手的写法
            if (key.StartsWith("flag:") || key.StartsWith("item:") || key.StartsWith("obj:"))
            {
                string captured = key;
                m_PanelTrace.Add(Button("＋ 用「它」当完成条件新建谜题", () => AddPuzzleWithCondition(captured)));
            }
        }

        /// <summary>这条规则读或写的任何东西，是不是 key 指的那个。</summary>
        private static bool Touches(InteractionRule rule, string key)
        {
            if (key.StartsWith("obj:") && rule.targetId == key.Substring(4))
            {
                return true;
            }

            if (key.StartsWith("item:") && rule.itemId == key.Substring(5))
            {
                return true;
            }

            for (int i = 0; i < rule.conditions.Count; i++)
            {
                if (ConditionTouches(rule.conditions[i], key))
                {
                    return true;
                }
            }

            for (int i = 0; i < rule.effects.Count; i++)
            {
                if (EffectTouches(rule.effects[i], key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConditionTouches(PuzzleCondition condition, string key)
        {
            switch (condition)
            {
                case FlagCondition flag: return key == "flag:" + flag.flag;
                case HasItemCondition item: return key == "item:" + item.itemId;
                case SelectedItemCondition item: return key == "item:" + item.itemId;
                case ObjectStateCondition state: return key == "obj:" + state.interactableId;
                case CharacterInEraCondition character: return key == "obj:" + character.characterId;
                case SelectedCharacterCondition character: return key == "obj:" + character.characterId;
                case AndCondition and:
                    for (int i = 0; i < and.items.Count; i++)
                    {
                        if (ConditionTouches(and.items[i], key)) { return true; }
                    }

                    return false;
                case OrCondition or:
                    for (int i = 0; i < or.items.Count; i++)
                    {
                        if (ConditionTouches(or.items[i], key)) { return true; }
                    }

                    return false;
                case NotCondition not: return ConditionTouches(not.item, key);
                default: return false;
            }
        }

        private static bool EffectTouches(PuzzleEffect effect, string key)
        {
            switch (effect)
            {
                case SetFlagEffect flag: return key == "flag:" + flag.flag;
                case AddFlagEffect flag: return key == "flag:" + flag.flag;
                case GiveItemEffect item: return key == "item:" + item.itemId;
                case ConsumeItemEffect item: return key == "item:" + item.itemId;
                case SelectItemEffect item: return key == "item:" + item.itemId;
                case SetObjectStateEffect state: return key == "obj:" + state.interactableId;
                case MoveCharacterEffect move: return key == "obj:" + move.characterId;
                case MoveCharacterStepEffect step: return key == "obj:" + step.characterId;
                case SelectCharacterEffect select: return key == "obj:" + select.characterId;
                case SolvePuzzleEffect solve: return key == "puzzle:" + solve.puzzleId;
                default: return false;
            }
        }

        // ===================== 规则字段（原生 Inspector 画法） =====================

        private void DrawRuleInspector(int index)
        {
            if (m_Serialized == null)
            {
                return;
            }

            if (m_Form == null)
            {
                m_Form = new PuzzleForm(m_Scan);
            }

            m_Form.SetPuzzleIds(PuzzleIds());

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");

            if (rules == null || index < 0 || index >= rules.arraySize)
            {
                return;
            }

            // 改优先级这类动数组的操作推迟到下一帧（别在 IMGUI 画到一半把数组换掉）
            bool changed = m_Form.Draw(rules.GetArrayElementAtIndex(index), index, offset => m_PendingMove = offset);

            if (changed)
            {
                m_Serialized.ApplyModifiedProperties();

                // 只更新那个规则节点的文字，不整张图重建（免得打字时图一直闪）
                RefreshRuleNode(index);

                m_Scan = PuzzleEditorScan.Scan(m_Table);
                m_Form.RefreshCandidates();
                ShowIssues(index);
            }
        }

        private List<string> PuzzleIds()
        {
            List<string> ids = new List<string>();

            if (m_Table == null)
            {
                return ids;
            }

            for (int i = 0; i < m_Table.puzzles.Count; i++)
            {
                PuzzleDefinition puzzle = m_Table.puzzles[i];
                if (puzzle != null && !string.IsNullOrEmpty(puzzle.id))
                {
                    ids.Add(puzzle.id);
                }
            }

            return ids;
        }

        private void RefreshRuleNode(int index)
        {
            if (m_Graph == null || !m_Graph.RuleNodes.TryGetValue(index, out PuzzleGraphNode node))
            {
                return;
            }

            InteractionRule rule = GetRule(index);
            if (rule == null)
            {
                return;
            }

            node.title = $"#{index + 1}  {(string.IsNullOrEmpty(rule.targetId) ? "*任意物体*" : rule.targetId)} · {VerbText(rule.verb)}";
            node.SetBody(RuleBodyText(rule));
        }

        private static string RuleBodyText(InteractionRule rule)
        {
            string conditions = rule.conditions == null || rule.conditions.Count == 0
                ? "条件：无条件"
                : "条件：" + Describe(rule.conditions);

            string effects = rule.effects == null || rule.effects.Count == 0
                ? "效果：无"
                : "效果：" + Describe(rule.effects);

            return conditions + "\n" + effects +
                   (string.IsNullOrEmpty(rule.elseFeedback) ? "" : $"\n不满足时：「{rule.elseFeedback}」");
        }

        private static string Describe(List<PuzzleCondition> conditions)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < conditions.Count; i++)
            {
                parts.Add(conditions[i] != null ? conditions[i].Describe() : "(空)");
            }

            return string.Join(" 且 ", parts);
        }

        private static string Describe(List<PuzzleEffect> effects)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                parts.Add(effects[i] != null ? effects[i].Describe() : "(空)");
            }

            return string.Join(" → ", parts);
        }

        // ===================== 规则增删改序 =====================

        private void MoveRule(int from, int to)
        {
            if (m_Serialized == null || m_Table == null || to < 0 || to >= m_Table.interactionRules.Count)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");
            rules.MoveArrayElement(from, to);
            m_Serialized.ApplyModifiedProperties();
            m_SelectedRule = to;
            RebuildAll();
            FocusRule(to);
        }

        private void DuplicateRule(int index)
        {
            if (m_Serialized == null)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");
            rules.InsertArrayElementAtIndex(index);
            m_Serialized.ApplyModifiedProperties();

            RebuildAll();
            FocusRule(index + 1);
        }

        private void DeleteRule(int index)
        {
            if (m_Serialized == null)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");

            if (index < 0 || index >= rules.arraySize)
            {
                return;
            }

            rules.DeleteArrayElementAtIndex(index);
            m_Serialized.ApplyModifiedProperties();

            RebuildAll();
            Log($"已删除规则 #{index + 1} —— Ctrl+Z（或工具栏的 ↶ 撤回）可以撤销");
        }

        private void AddRule(string targetId)
        {
            if (m_Serialized == null)
            {
                return;
            }

            m_Serialized.Update();
            SerializedProperty rules = m_Serialized.FindProperty("interactionRules");
            int index = rules.arraySize;
            rules.InsertArrayElementAtIndex(index);

            // 新元素是复制出来的，清干净
            SerializedProperty added = rules.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("note").stringValue = "新规则";
            added.FindPropertyRelative("targetId").stringValue = targetId ?? "";
            added.FindPropertyRelative("verb").intValue = (int)Verb.Interact;
            added.FindPropertyRelative("itemId").stringValue = "";
            added.FindPropertyRelative("elseFeedback").stringValue = "";
            added.FindPropertyRelative("conditions").arraySize = 0;
            added.FindPropertyRelative("effects").arraySize = 0;

            m_Serialized.ApplyModifiedProperties();
            Log($"已新建规则 #{index + 1}（目标留空）—— 记得在右边填「点了哪个物体」。");

            RebuildAll();
            FocusRule(index);
        }

        private void FocusRule(int index)
        {
            if (m_Graph == null || !m_Graph.RuleNodes.TryGetValue(index, out PuzzleGraphNode node))
            {
                return;
            }

            m_Graph.ClearSelection();
            m_Graph.AddToSelection(node);
            m_Graph.FrameSelection();
            m_LastSignature = "";
            RefreshPanel();
        }

        private void SelectTarget(string targetId)
        {
            PuzzleGraphNode node = m_Graph != null ? m_Graph.Get("obj:" + targetId) : null;
            if (node == null)
            {
                return;
            }

            m_Graph.ClearSelection();
            m_Graph.AddToSelection(node);
            m_LastSignature = "";
            RefreshPanel();
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

        // ===================== 试跑 =====================

        private void RunTrace(string targetId, bool everythingOn)
        {
            m_PanelTrace.Clear();

            PuzzleSimulation simulation = new PuzzleSimulation();
            simulation.FocusedEraIndex = -1;

            Dictionary<string, float> flags = new Dictionary<string, float>();
            foreach (string flag in m_Scan.WrittenFlags)
            {
                flags[flag] = everythingOn ? 1f : 0f;
            }

            foreach (string flag in m_Scan.ReadFlags)
            {
                if (!flags.ContainsKey(flag))
                {
                    flags[flag] = everythingOn ? 1f : 0f;
                }
            }

            HashSet<string> items = everythingOn ? m_Scan.GivenItems : new HashSet<string>();

            Dictionary<string, EraId> characters = new Dictionary<string, EraId>();
            CharacterView[] views = Object.FindObjectsOfType<CharacterView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null)
                {
                    characters[views[i].CharacterId] = views[i].HomeEra;
                }
            }

            simulation.Apply(flags, items, everythingOn && items.Count > 0 ? First(items) : "", characters, "");

            List<string> trace = simulation.Trace(m_Table, targetId, Verb.Interact, "");

            Label header = new Label(everythingOn ? "试跑（所有 flag 打开 / 背包全满）" : "试跑（初始状态）");
            header.style.fontSize = 10f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 8f;
            header.style.paddingTop = 6f;
            m_PanelTrace.Add(header);

            for (int i = 0; i < trace.Count; i++)
            {
                Label line = new Label(trace[i]);
                line.style.fontSize = 10f;
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.paddingLeft = 8f;
                m_PanelTrace.Add(line);
            }
        }

        private static string First(HashSet<string> source)
        {
            foreach (string value in source)
            {
                return value;
            }

            return "";
        }

        // ===================== 问题列表 =====================

        private void ShowIssues(int ruleIndex)
        {
            m_PanelIssues.Clear();

            if (m_Scan == null || m_Scan.Issues.Count == 0)
            {
                return;
            }

            Label header = new Label($"校验问题（{m_Scan.Errors} 错 / {m_Scan.Warnings} 提醒）");
            header.style.fontSize = 10f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 8f;
            header.style.paddingTop = 8f;
            m_PanelIssues.Add(header);

            for (int i = 0; i < m_Scan.Issues.Count; i++)
            {
                PuzzleIssue issue = m_Scan.Issues[i];

                // 选中某条规则时优先显示它自己的问题
                bool own = ruleIndex >= 0 && issue.RuleIndex == ruleIndex;
                if (ruleIndex >= 0 && !own)
                {
                    continue;
                }

                string prefix = issue.RuleIndex >= 0 ? $"#{issue.RuleIndex + 1}：" : "";
                Label label = new Label((issue.IsError ? "❌ " : "⚠ ") + prefix + issue.Message);
                label.style.fontSize = 10f;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.paddingLeft = 8f;
                label.style.paddingRight = 8f;
                label.style.marginTop = 2f;
                label.style.color = issue.IsError
                    ? new Color(1f, 0.55f, 0.5f)
                    : new Color(1f, 0.82f, 0.45f);
                m_PanelIssues.Add(label);
            }

            if (ruleIndex < 0 && m_Table != null && m_ShowRules)
            {
                m_PanelIssues.Add(Button("新建一条规则（目标留空）", () => AddRule("")));
            }
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
    }
}
