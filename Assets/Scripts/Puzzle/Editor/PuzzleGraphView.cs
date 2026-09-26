using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 谜题关系图。
    ///
    /// 关键设计：**规则本身就是一个节点**，不是边。因为规则表是"顺序查表、第一条命中的生效"，
    /// 顺序用**同一列的上下位置**表达最直观（越上面优先级越高）；边只表示"读了什么 / 写了什么"。
    /// 如果把规则做成边，"多条规则 + 谁先命中"就没地方表达了。
    ///
    /// 列：物体/人物 → 规则 → flag/道具 → 谜题 → 时代
    /// </summary>
    public class PuzzleGraphView : GraphView
    {
        public readonly Dictionary<string, PuzzleGraphNode> Nodes = new Dictionary<string, PuzzleGraphNode>();
        public readonly Dictionary<int, PuzzleGraphNode> RuleNodes = new Dictionary<int, PuzzleGraphNode>();

        public PuzzleGraphView()
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

        /// <summary>返回空 = 拖不出新连线（连线是规则算出来的，不是手画的）。</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) => new List<Port>();

        public void ClearAll()
        {
            DeleteElements(graphElements.ToList());
            Nodes.Clear();
            RuleNodes.Clear();
        }

        public void AddNode(PuzzleGraphNode node)
        {
            AddElement(node);

            if (!string.IsNullOrEmpty(node.Key))
            {
                Nodes[node.Key] = node;
            }

            if (node.RuleIndex >= 0)
            {
                RuleNodes[node.RuleIndex] = node;
            }
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
        }

        public PuzzleGraphNode Get(string key) =>
            !string.IsNullOrEmpty(key) && Nodes.TryGetValue(key, out PuzzleGraphNode node) ? node : null;
    }

    /// <summary>图上的一个节点。规则节点（RuleIndex >= 0）和状态节点用同一个类。</summary>
    public class PuzzleGraphNode : Node
    {
        public string Key;
        public int RuleIndex = -1;
        public Port In;
        public Port Out;

        private readonly Label m_Body;

        public PuzzleGraphNode(string key, int ruleIndex, string title, string body, Color color)
        {
            Key = key;
            RuleIndex = ruleIndex;
            this.title = title;

            In = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Out = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            In.portName = "";
            Out.portName = "";
            inputContainer.Add(In);
            outputContainer.Add(Out);

            m_Body = new Label(body ?? "");
            m_Body.style.fontSize = 10f;
            m_Body.style.color = new Color(0.80f, 0.80f, 0.82f);
            m_Body.style.whiteSpace = WhiteSpace.Normal;
            m_Body.style.maxWidth = 240f;
            mainContainer.Add(m_Body);

            titleContainer.style.backgroundColor = color;

            if (ruleIndex >= 0)
            {
                // 规则节点给个更醒目的标题栏
                titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // 能拖动摆位置，但不能复制 / 删除 / 改大小（删规则走右侧面板，免得不小心删掉）
            capabilities &= ~(Capabilities.Copiable | Capabilities.Deletable | Capabilities.Resizable);
        }

        public void SetBody(string body)
        {
            if (m_Body != null)
            {
                m_Body.text = body ?? "";
            }
        }
    }

    /// <summary>把规则表翻译成节点 + 边。</summary>
    public static class PuzzleGraphBuilder
    {
        private static readonly Color ObjectColor = new Color(0.20f, 0.29f, 0.40f);
        private static readonly Color CharacterColor = new Color(0.24f, 0.34f, 0.30f);
        private static readonly Color RuleColor = new Color(0.34f, 0.30f, 0.20f);
        private static readonly Color FlagColor = new Color(0.45f, 0.28f, 0.14f);
        private static readonly Color ItemColor = new Color(0.20f, 0.36f, 0.23f);
        private static readonly Color PuzzleColor = new Color(0.33f, 0.23f, 0.45f);
        private static readonly Color EraColor = new Color(0.17f, 0.35f, 0.38f);

        public static void Build(PuzzleGraphView graph, PuzzleTableSO table, PuzzleScanResult scan,
            Dictionary<string, Rect> layout, bool showObjects, bool showRules, bool showFlags, bool showItems,
            bool showPuzzles, bool showEras)
        {
            // 记住用户手动摆过的位置（一次会话内）
            Dictionary<string, Rect> previous = new Dictionary<string, Rect>();
            foreach (GraphElement element in graph.graphElements.ToList())
            {
                if (element is PuzzleGraphNode kept && kept.RuleIndex < 0 && !string.IsNullOrEmpty(kept.Key))
                {
                    previous[kept.Key] = kept.GetPosition();
                }
            }

            graph.ClearAll();

            if (table == null)
            {
                return;
            }

            // ---- 状态类节点 ----
            if (showObjects)
            {
                foreach (string id in Sorted(scan.InteractionIds))
                {
                    bool character = scan.CharacterIds.Contains(id);
                    graph.AddNode(new PuzzleGraphNode("obj:" + id, -1, id, character ? "人物" : "物体",
                        character ? CharacterColor : ObjectColor));
                }
            }

            if (showItems)
            {
                foreach (string item in Sorted(Union(scan.GivenItems, scan.UsedItems)))
                {
                    string body = scan.GivenItems.Contains(item) ? "能拿到" : "拿不到（没人给）";
                    graph.AddNode(new PuzzleGraphNode("item:" + item, -1, item, body, ItemColor));
                }
            }

            if (showFlags)
            {
                foreach (string flag in Sorted(Union(scan.WrittenFlags, scan.ReadFlags)))
                {
                    string body = $"写：{(scan.WrittenFlags.Contains(flag) ? "有" : "无")}    读：{(scan.ReadFlags.Contains(flag) ? "有" : "无")}";
                    graph.AddNode(new PuzzleGraphNode("flag:" + flag, -1, flag, body, FlagColor));
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

                    string title = string.IsNullOrEmpty(puzzle.title) ? puzzle.id : puzzle.title;
                    string head = puzzle.isMainPuzzle ? "主线：解开 = 这个时代通关" : "支线";
                    string body = puzzle.mode == PuzzleMode.Steps
                        ? $"{head}\n步骤式 {puzzle.steps.Count} 步{(puzzle.stepsInOrder ? "（按顺序）" : "（任意顺序）")}"
                        : $"{head}\n条件式 {puzzle.conditions.Count} 条";
                    graph.AddNode(new PuzzleGraphNode("puzzle:" + puzzle.id, -1, title, body, PuzzleColor));
                }
            }

            if (showEras)
            {
                for (int i = 0; i < EraCatalog.All.Length; i++)
                {
                    EraPresentation era = EraCatalog.All[i];
                    graph.AddNode(new PuzzleGraphNode("era:" + era.id, -1, era.title, era.timeline, EraColor));
                }
            }

            // ---- 规则节点（顺序 = 优先级） ----
            if (showRules)
            {
                for (int i = 0; i < table.interactionRules.Count; i++)
                {
                    InteractionRule rule = table.interactionRules[i];
                    if (rule == null)
                    {
                        continue;
                    }

                    string target = string.IsNullOrEmpty(rule.targetId) ? "*任意物体*" : rule.targetId;
                    string verb = VerbText(rule.verb) + (string.IsNullOrEmpty(rule.itemId) ? "" : " " + rule.itemId);
                    string title = $"#{i + 1}  {target} · {verb}";
                    graph.AddNode(new PuzzleGraphNode("rule:" + i, i, title, RuleBody(rule), RuleColor));
                }
            }

            // ---- 边 ----
            for (int i = 0; i < table.interactionRules.Count; i++)
            {
                InteractionRule rule = table.interactionRules[i];
                if (rule == null)
                {
                    continue;
                }

                PuzzleGraphNode ruleNode = graph.Get("rule:" + i);
                if (ruleNode == null)
                {
                    continue;
                }

                PuzzleGraphNode target = graph.Get("obj:" + rule.targetId);

                // 「点它 → 这条规则」
                if (target != null)
                {
                    graph.AddEdge(target, ruleNode);
                }

                // 道具门槛：道具 → 规则
                if (!string.IsNullOrEmpty(rule.itemId))
                {
                    graph.AddEdge(graph.Get("item:" + rule.itemId), ruleNode);
                }

                // 读：状态 → 规则
                for (int c = 0; c < rule.conditions.Count; c++)
                {
                    LinkReads(rule.conditions[c], graph, ruleNode);
                }

                // 写：规则 → 状态
                for (int e = 0; e < rule.effects.Count; e++)
                {
                    LinkWrites(rule.effects[e], graph, ruleNode);
                }
            }

            // ---- 谜题：完成条件的来源 → 谜题；主线谜题 → 时代 ----
            for (int i = 0; i < table.puzzles.Count; i++)
            {
                PuzzleDefinition puzzle = table.puzzles[i];
                if (puzzle == null)
                {
                    continue;
                }

                PuzzleGraphNode node = graph.Get("puzzle:" + puzzle.id);
                if (node == null)
                {
                    continue;
                }

                for (int c = 0; c < puzzle.conditions.Count; c++)
                {
                    LinkReads(puzzle.conditions[c], graph, node);
                }

                // 步骤式：每一步的条件也连到谜题上（依赖图照样看得出这条线）
                for (int s = 0; s < puzzle.steps.Count; s++)
                {
                    PuzzleStep step = puzzle.steps[s];
                    if (step == null)
                    {
                        continue;
                    }

                    for (int c = 0; c < step.conditions.Count; c++)
                    {
                        LinkReads(step.conditions[c], graph, node);
                    }
                }

                if (puzzle.isMainPuzzle)
                {
                    graph.AddEdge(node, graph.Get("era:" + puzzle.era));
                }
            }

            Layout(graph, layout, previous);
        }

        private static string RuleBody(InteractionRule rule)
        {
            string conditions = rule.conditions == null || rule.conditions.Count == 0
                ? "条件：无条件"
                : "条件：" + Join(rule.conditions);

            string effects = rule.effects == null || rule.effects.Count == 0
                ? "效果：无"
                : "效果：" + Join(rule.effects);

            string extra = string.IsNullOrEmpty(rule.elseFeedback) ? "" : $"\n不满足时：「{rule.elseFeedback}」";
            return conditions + "\n" + effects + extra;
        }

        private static string Join(List<PuzzleCondition> conditions)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < conditions.Count; i++)
            {
                parts.Add(conditions[i] != null ? conditions[i].Describe() : "(空)");
            }

            return string.Join(" 且 ", parts);
        }

        private static string Join(List<PuzzleEffect> effects)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                parts.Add(effects[i] != null ? effects[i].Describe() : "(空)");
            }

            return string.Join(" → ", parts);
        }

        private static void LinkReads(PuzzleCondition condition, PuzzleGraphView graph, PuzzleGraphNode to)
        {
            switch (condition)
            {
                case FlagCondition flag:
                    graph.AddEdge(graph.Get("flag:" + flag.flag), to);
                    return;

                case HasItemCondition item:
                    graph.AddEdge(graph.Get("item:" + item.itemId), to);
                    return;

                case SelectedItemCondition item:
                    graph.AddEdge(graph.Get("item:" + item.itemId), to);
                    return;

                case ObjectStateCondition state:
                    graph.AddEdge(graph.Get("obj:" + state.interactableId), to);
                    return;

                case CharacterInEraCondition character:
                    graph.AddEdge(graph.Get("obj:" + character.characterId), to);
                    return;

                case SelectedCharacterCondition character:
                    graph.AddEdge(graph.Get("obj:" + character.characterId), to);
                    return;

                case AndCondition and:
                    for (int i = 0; i < and.items.Count; i++)
                    {
                        LinkReads(and.items[i], graph, to);
                    }

                    return;

                case OrCondition or:
                    for (int i = 0; i < or.items.Count; i++)
                    {
                        LinkReads(or.items[i], graph, to);
                    }

                    return;

                case NotCondition not:
                    LinkReads(not.item, graph, to);
                    return;
            }
        }

        private static void LinkWrites(PuzzleEffect effect, PuzzleGraphView graph, PuzzleGraphNode from)
        {
            switch (effect)
            {
                case SetFlagEffect flag:
                    graph.AddEdge(from, graph.Get("flag:" + flag.flag));
                    return;

                case AddFlagEffect flag:
                    graph.AddEdge(from, graph.Get("flag:" + flag.flag));
                    return;

                case GiveItemEffect item:
                    graph.AddEdge(from, graph.Get("item:" + item.itemId));
                    return;

                case SetObjectStateEffect state:
                    graph.AddEdge(from, graph.Get("obj:" + state.interactableId));
                    return;

                case MoveCharacterEffect move:
                    graph.AddEdge(from, graph.Get("obj:" + move.characterId));
                    graph.AddEdge(from, graph.Get("era:" + move.targetEra));
                    return;

                case MoveCharacterStepEffect step:
                    graph.AddEdge(from, graph.Get("obj:" + step.characterId));
                    return;

                case MoveSelectedCharacterEffect move:
                    graph.AddEdge(from, graph.Get("era:" + move.targetEra));
                    return;

                case MoveEraCharactersEffect move:
                    graph.AddEdge(from, graph.Get("era:" + move.targetEra));
                    return;

                case SelectCharacterEffect select:
                    graph.AddEdge(from, graph.Get("obj:" + select.characterId));
                    return;
            }
        }

        // ===================== 布局 =====================

        /// <summary>按类型分列。规则那一列按表里的顺序从上往下排 = 优先级从上往下。</summary>
        private static void Layout(PuzzleGraphView graph, Dictionary<string, Rect> layout,
            Dictionary<string, Rect> previous)
        {
            Dictionary<int, int> cursor = new Dictionary<int, int>();

            foreach (GraphElement element in graph.graphElements.ToList())
            {
                if (!(element is PuzzleGraphNode node))
                {
                    continue;
                }

                // 状态节点保留用户摆过的位置；规则节点永远按优先级重排
                if (node.RuleIndex < 0 && previous.TryGetValue(node.Key, out Rect saved))
                {
                    node.SetPosition(saved);
                    continue;
                }

                int column = ColumnOf(node);
                cursor.TryGetValue(column, out int row);
                cursor[column] = row + 1;

                Rect rect = new Rect(column * 300f, row * 132f, 260f, 90f);
                node.SetPosition(rect);

                if (node.RuleIndex < 0)
                {
                    layout[node.Key] = rect;
                }
            }
        }

        private static int ColumnOf(PuzzleGraphNode node)
        {
            if (node.RuleIndex >= 0)
            {
                return 1;
            }

            if (node.Key.StartsWith("obj:")) return 0;
            if (node.Key.StartsWith("flag:")) return 2;
            if (node.Key.StartsWith("item:")) return 2;
            if (node.Key.StartsWith("puzzle:")) return 3;
            return 4;
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
