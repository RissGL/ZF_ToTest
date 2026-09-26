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
    /// 「谁写了这个 flag、谁又读了它」的关系图。
    ///
    /// 这是**只读的分析视图**，不是编辑器 —— 谜题规则本质是「顺序查表」，画成节点图反而难读；
    /// 但「跨时代联动」「死锁（没人写 / 没人读）」「完成链」这些**关系**，图比表直观得多。
    ///
    /// 菜单：Tools → 谜题 → 依赖关系图
    /// </summary>
    public class PuzzleDependencyWindow : EditorWindow
    {
        private const string LastTableKey = "ZF.Puzzle.LastTable";

        private static readonly Dictionary<string, Rect> s_Layout = new Dictionary<string, Rect>();

        private PuzzleDependencyGraph m_Graph;
        private PuzzleTableSO m_Table;
        private PuzzleScanResult m_Scan;
        private Label m_Summary;

        private bool m_ShowObjects = true;
        private bool m_ShowFlags = true;
        private bool m_ShowItems = true;
        private bool m_ShowPuzzles = true;
        private bool m_ShowEras = true;

        [MenuItem("Tools/谜题/依赖关系图", false, 31)]
        public static void Open()
        {
            PuzzleDependencyWindow window = GetWindow<PuzzleDependencyWindow>("谜题依赖图");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 6f;
            toolbar.style.paddingRight = 6f;
            toolbar.style.paddingTop = 4f;
            toolbar.style.paddingBottom = 4f;
            root.Add(toolbar);

            ObjectField tableField = new ObjectField("规则表")
            {
                objectType = typeof(PuzzleTableSO),
                allowSceneObjects = false,
                value = ResolveTable(),
                style = { width = 280f },
            };
            tableField.RegisterValueChangedCallback(evt => SetTable(evt.newValue as PuzzleTableSO));
            toolbar.Add(tableField);

            toolbar.Add(MakeButton("重新扫描", () => Rebuild()));
            toolbar.Add(MakeButton("重新布局", () =>
            {
                s_Layout.Clear();
                Rebuild();
            }));
            toolbar.Add(MakeButton("居中", () => m_Graph?.FrameAll()));

            toolbar.Add(new Label("  显示：") { style = { marginLeft = 8f } });
            toolbar.Add(MakeToggle("物体/人物", m_ShowObjects, value => { m_ShowObjects = value; Rebuild(); }));
            toolbar.Add(MakeToggle("flag", m_ShowFlags, value => { m_ShowFlags = value; Rebuild(); }));
            toolbar.Add(MakeToggle("道具", m_ShowItems, value => { m_ShowItems = value; Rebuild(); }));
            toolbar.Add(MakeToggle("谜题", m_ShowPuzzles, value => { m_ShowPuzzles = value; Rebuild(); }));
            toolbar.Add(MakeToggle("时代", m_ShowEras, value => { m_ShowEras = value; Rebuild(); }));

            m_Summary = new Label();
            m_Summary.style.marginLeft = 10f;
            m_Summary.style.fontSize = 11f;
            toolbar.Add(m_Summary);

            m_Graph = new PuzzleDependencyGraph();
            m_Graph.style.flexGrow = 1f;
            root.Add(m_Graph);

            m_Table = tableField.value as PuzzleTableSO;
            Rebuild();
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

        private void SetTable(PuzzleTableSO table)
        {
            m_Table = table;

            if (m_Table != null)
            {
                EditorPrefs.SetString(LastTableKey, AssetDatabase.GetAssetPath(m_Table));
            }

            s_Layout.Clear();
            Rebuild();
        }

        private void Rebuild()
        {
            if (m_Graph == null)
            {
                return;
            }

            m_Scan = PuzzleEditorScan.Scan(m_Table);
            PuzzleDependencyGraphBuilder.Build(m_Graph, m_Table, m_Scan, s_Layout,
                m_ShowObjects, m_ShowFlags, m_ShowItems, m_ShowPuzzles, m_ShowEras);

            if (m_Summary != null)
            {
                m_Summary.text = m_Table == null
                    ? "没有规则表"
                    : $"{m_Graph.NodeCount} 个节点 / {m_Graph.EdgeCount} 条关系" +
                      (m_Scan.Errors > 0 ? $" · ❌ {m_Scan.Errors} 个错误" : "") +
                      (m_Scan.Warnings > 0 ? $" · ⚠ {m_Scan.Warnings} 个提醒" : "");
            }
        }

        private static Button MakeButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick) { text = text };
            button.style.marginLeft = 4f;
            return button;
        }

        private static Toggle MakeToggle(string text, bool value, System.Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(text) { value = value };
            toggle.style.marginLeft = 4f;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }
    }

    /// <summary>只读图：能缩放平移、能拖动节点摆位置，但不能连线、不能删。</summary>
    public class PuzzleDependencyGraph : GraphView
    {
        public int NodeCount { get; private set; }
        public int EdgeCount { get; private set; }

        public PuzzleDependencyGraph()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        /// <summary>返回空 = 用户拖不出新连线（这是只读视图）。</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) => new List<Port>();

        public void ClearAll()
        {
            DeleteElements(graphElements.ToList());
            NodeCount = 0;
            EdgeCount = 0;
        }

        public void AddNode(PuzzleGraphNode node)
        {
            AddElement(node);
            NodeCount++;
        }

        public void AddEdge(PuzzleGraphNode from, PuzzleGraphNode to)
        {
            if (from == null || to == null || from == to)
            {
                return;
            }

            Edge edge = new Edge { input = to.In, output = from.Out };
            edge.input.Connect(edge);
            edge.output.Connect(edge);
            AddElement(edge);
            EdgeCount++;
        }
    }

    public class PuzzleGraphNode : Node
    {
        public string Key;
        public Port In;
        public Port Out;

        public PuzzleGraphNode(string key, string title, string subtitle, Color color)
        {
            Key = key;
            this.title = title;

            In = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Out = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            In.portName = "";
            Out.portName = "";
            inputContainer.Add(In);
            outputContainer.Add(Out);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label label = new Label(subtitle);
                label.style.fontSize = 10f;
                label.style.color = new Color(0.78f, 0.78f, 0.78f);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.maxWidth = 200f;
                mainContainer.Add(label);
            }

            titleContainer.style.backgroundColor = color;

            // 可以拖动摆位置（好看），但不能复制 / 删除（只读）
            capabilities &= ~(Capabilities.Copiable | Capabilities.Deletable | Capabilities.Resizable);
        }
    }

    /// <summary>把规则表翻译成节点 + 边。</summary>
    public static class PuzzleDependencyGraphBuilder
    {
        private static readonly Color ObjectColor = new Color(0.22f, 0.30f, 0.40f);
        private static readonly Color FlagColor = new Color(0.45f, 0.30f, 0.16f);
        private static readonly Color ItemColor = new Color(0.20f, 0.36f, 0.24f);
        private static readonly Color PuzzleColor = new Color(0.34f, 0.24f, 0.46f);
        private static readonly Color EraColor = new Color(0.18f, 0.36f, 0.38f);

        public static void Build(PuzzleDependencyGraph graph, PuzzleTableSO table, PuzzleScanResult scan,
            Dictionary<string, Rect> layout, bool showObjects, bool showFlags, bool showItems, bool showPuzzles, bool showEras)
        {
            // 记住用户手动摆过的位置（一次会话内）
            Dictionary<string, Rect> previous = new Dictionary<string, Rect>();
            foreach (GraphElement element in graph.graphElements.ToList())
            {
                if (element is PuzzleGraphNode node && !string.IsNullOrEmpty(node.Key))
                {
                    previous[node.Key] = node.GetPosition();
                }
            }

            graph.ClearAll();

            if (table == null)
            {
                return;
            }

            Dictionary<string, PuzzleGraphNode> nodes = new Dictionary<string, PuzzleGraphNode>();

            // ---- 节点 ----
            if (showObjects)
            {
                foreach (string id in Sorted(scan.InteractionIds))
                {
                    bool isCharacter = scan.CharacterIds.Contains(id);
                    Add(nodes, graph, "obj:" + id, id,
                        isCharacter ? "人物" : "物体", ObjectColor);
                }
            }

            if (showFlags)
            {
                foreach (string flag in Sorted(Union(scan.WrittenFlags, scan.ReadFlags)))
                {
                    Add(nodes, graph, "flag:" + flag, flag,
                        $"写 {(scan.WrittenFlags.Contains(flag) ? "有" : "无")} · 读 {(scan.ReadFlags.Contains(flag) ? "有" : "无")}",
                        FlagColor);
                }
            }

            if (showItems)
            {
                foreach (string item in Sorted(Union(scan.GivenItems, scan.UsedItems)))
                {
                    Add(nodes, graph, "item:" + item, item,
                        scan.GivenItems.Contains(item) ? "能拿到" : "拿不到（没人给）", ItemColor);
                }
            }

            if (showPuzzles)
            {
                for (int i = 0; i < table.puzzles.Count; i++)
                {
                    PuzzleDefinition puzzle = table.puzzles[i];
                    if (puzzle == null || string.IsNullOrEmpty(puzzle.id))
                    {
                        continue;
                    }

                    Add(nodes, graph, "puzzle:" + puzzle.id,
                        string.IsNullOrEmpty(puzzle.title) ? puzzle.id : puzzle.title,
                        puzzle.isMainPuzzle ? "主线（解开 = 这个时代通关）" : $"{puzzle.era}", PuzzleColor);
                }
            }

            if (showEras)
            {
                for (int i = 0; i < EraCatalog.All.Length; i++)
                {
                    EraPresentation era = EraCatalog.All[i];
                    Add(nodes, graph, "era:" + era.id, era.title, era.timeline, EraColor);
                }
            }

            // ---- 边：逐条规则看它「读什么 → 写什么」 ----
            for (int i = 0; i < table.interactionRules.Count; i++)
            {
                InteractionRule rule = table.interactionRules[i];
                if (rule == null)
                {
                    continue;
                }

                PuzzleGraphNode target = Lookup(nodes, "obj:" + rule.targetId);
                PuzzleGraphNode item = Lookup(nodes, "item:" + rule.itemId);

                // 用道具 → 目标
                if (item != null && target != null)
                {
                    graph.AddEdge(item, target);
                }

                // 读：flag → 目标（"这个物体要等这个 flag 才动"）
                for (int c = 0; c < rule.conditions.Count; c++)
                {
                    CollectReads(rule.conditions[c], nodes, target, graph);
                }

                // 写：目标 → flag / 物体 / 人物 / 时代
                for (int e = 0; e < rule.effects.Count; e++)
                {
                    CollectWrites(rule.effects[e], nodes, target, graph);
                }
            }

            // ---- 边：flag → 谜题（完成条件） ----
            for (int i = 0; i < table.puzzles.Count; i++)
            {
                PuzzleDefinition puzzle = table.puzzles[i];
                if (puzzle == null)
                {
                    continue;
                }

                PuzzleGraphNode node = Lookup(nodes, "puzzle:" + puzzle.id);
                if (node == null)
                {
                    continue;
                }

                for (int c = 0; c < puzzle.conditions.Count; c++)
                {
                    CollectReads(puzzle.conditions[c], nodes, node, graph);
                }

                if (puzzle.isMainPuzzle)
                {
                    graph.AddEdge(node, Lookup(nodes, "era:" + puzzle.era));
                }
            }

            Layout(graph, layout, previous);
        }

        private static void CollectReads(PuzzleCondition condition, Dictionary<string, PuzzleGraphNode> nodes,
            PuzzleGraphNode target, PuzzleDependencyGraph graph)
        {
            switch (condition)
            {
                case FlagCondition flag:
                    graph.AddEdge(Lookup(nodes, "flag:" + flag.flag), target);
                    return;

                case HasItemCondition item:
                    graph.AddEdge(Lookup(nodes, "item:" + item.itemId), target);
                    return;

                case ObjectStateCondition state:
                    graph.AddEdge(Lookup(nodes, "obj:" + state.interactableId), target);
                    return;

                case CharacterInEraCondition character:
                    graph.AddEdge(Lookup(nodes, "obj:" + character.characterId), target);
                    return;

                case AndCondition and:
                    for (int i = 0; i < and.items.Count; i++)
                    {
                        CollectReads(and.items[i], nodes, target, graph);
                    }

                    return;

                case OrCondition or:
                    for (int i = 0; i < or.items.Count; i++)
                    {
                        CollectReads(or.items[i], nodes, target, graph);
                    }

                    return;

                case NotCondition not:
                    CollectReads(not.item, nodes, target, graph);
                    return;
            }
        }

        private static void CollectWrites(PuzzleEffect effect, Dictionary<string, PuzzleGraphNode> nodes,
            PuzzleGraphNode target, PuzzleDependencyGraph graph)
        {
            switch (effect)
            {
                case SetFlagEffect flag:
                    graph.AddEdge(target, Lookup(nodes, "flag:" + flag.flag));
                    return;

                case AddFlagEffect flag:
                    graph.AddEdge(target, Lookup(nodes, "flag:" + flag.flag));
                    return;

                case GiveItemEffect item:
                    graph.AddEdge(target, Lookup(nodes, "item:" + item.itemId));
                    return;

                case SetObjectStateEffect state:
                    graph.AddEdge(target, Lookup(nodes, "obj:" + state.interactableId));
                    return;

                case MoveCharacterEffect move:
                    graph.AddEdge(target, Lookup(nodes, "obj:" + move.characterId));
                    graph.AddEdge(target, Lookup(nodes, "era:" + move.targetEra));
                    return;

                case MoveSelectedCharacterEffect move:
                    graph.AddEdge(target, Lookup(nodes, "era:" + move.targetEra));
                    return;

                case MoveEraCharactersEffect move:
                    graph.AddEdge(target, Lookup(nodes, "era:" + move.targetEra));
                    return;

                case SelectCharacterEffect select:
                    graph.AddEdge(target, Lookup(nodes, "obj:" + select.characterId));
                    return;
            }
        }

        // ===================== 布局 =====================

        /// <summary>按类型分列，纵向排。用户手动摆过的位置会优先保留。</summary>
        private static void Layout(PuzzleDependencyGraph graph, Dictionary<string, Rect> layout,
            Dictionary<string, Rect> previous)
        {
            Dictionary<int, int> columnCursor = new Dictionary<int, int>();

            foreach (GraphElement element in graph.graphElements.ToList())
            {
                if (!(element is PuzzleGraphNode node))
                {
                    continue;
                }

                if (previous.TryGetValue(node.Key, out Rect saved))
                {
                    node.SetPosition(saved);
                    continue;
                }

                int column = ColumnOf(node.Key);
                columnCursor.TryGetValue(column, out int row);
                columnCursor[column] = row + 1;

                Rect rect = new Rect(column * 260f, row * 90f, 220f, 60f);
                node.SetPosition(rect);
                layout[node.Key] = rect;
            }
        }

        private static int ColumnOf(string key)
        {
            if (key.StartsWith("obj:")) return 0;
            if (key.StartsWith("item:")) return 1;
            if (key.StartsWith("flag:")) return 2;
            if (key.StartsWith("puzzle:")) return 3;
            return 4;
        }

        // ===================== 杂项 =====================

        private static void Add(Dictionary<string, PuzzleGraphNode> nodes, PuzzleDependencyGraph graph,
            string key, string title, string subtitle, Color color)
        {
            if (nodes.ContainsKey(key))
            {
                return;
            }

            PuzzleGraphNode node = new PuzzleGraphNode(key, title, subtitle, color);
            nodes[key] = node;
            graph.AddNode(node);
        }

        private static PuzzleGraphNode Lookup(Dictionary<string, PuzzleGraphNode> nodes, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return nodes.TryGetValue(key, out PuzzleGraphNode node) ? node : null;
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
    }
}
