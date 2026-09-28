using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话图视图：承载"分组框（Group 容器）+ 组头节点 + 对话 item 节点"，
    /// 负责连线规则与"存/读 SO"。
    /// 分组方式：把节点拖进分组框里 = 视觉归组（保存时按"节点中心是否落在框内"记录归属）。
    /// </summary>
    public partial class DialogueGraphView : GraphView
    {
        public DialogueGraphData Data { get; private set; }

        public DialogueGraphView()
        {
            // 网格背景
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 缩放 / 拖拽 / 框选
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // 复制粘贴（Ctrl+C / Ctrl+V）：GraphView 默认不支持，必须自己实现序列化
            serializeGraphElements = SerializeForClipboard;
            canPasteSerializedData = CanPasteFromClipboard;
            unserializeAndPaste = PasteFromClipboard;

            // 增删连线 / 拖动都会回调这里，用来记脏和压撤销栈
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary>
        /// 连线规则：只能"输出 → 输入"，不能自己连自己。
        /// ★ 端口来源取两边的并集：
        ///   ① GraphView 自己的端口缓存（最权威）
        ///   ② 自己递归收集（节点被装进分组框、或者端口是建好节点之后动态加的，比如组头的选项端口，
        ///      这种情况下缓存里不一定有）
        /// 只信一边都可能出现"拖上去连不上"，所以两边都收。
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            var seen = new HashSet<Port>();

            ports.ForEach(port => TryAddCompatible(startPort, port, compatible, seen));

            foreach (var port in CollectAll<Port>())
            {
                TryAddCompatible(startPort, port, compatible, seen);
            }

            return compatible;
        }

        private static void TryAddCompatible(Port startPort, Port port, List<Port> result, HashSet<Port> seen)
        {
            if (port == null || port == startPort) return;
            if (port.node == null || port.node == startPort.node) return;
            if (port.direction == startPort.direction) return;
            if (!seen.Add(port)) return;

            result.Add(port);
        }

        /// <summary>
        /// 递归收集图里的元素（含被装进分组框里的节点、以及节点里嵌套的端口）。
        /// 走的是真实层级 hierarchy（不是 Children()，那是 contentContainer 的孩子）。
        /// 不用 GraphView.nodes / graphElements 这类 UQuery 属性：取法不受我们控制，
        /// 节点装进分组框之后可能枚举不到。
        /// </summary>
        private IEnumerable<T> CollectAll<T>() where T : VisualElement
        {
            var stack = new Stack<VisualElement>();
            stack.Push(contentViewContainer);

            while (stack.Count > 0)
            {
                var element = stack.Pop();
                var hierarchy = element.hierarchy;
                int childCount = hierarchy.childCount;

                for (int i = 0; i < childCount; i++)
                {
                    var child = hierarchy[i];
                    stack.Push(child);

                    if (child is T typed)
                    {
                        yield return typed;
                    }
                }
            }
        }

        /// <summary>
        /// 图上所有的连线：GraphView.edges 属性 + 递归收集 的并集（去重）。
        /// 用并集是因为"保存时一条线都没写进去"这种问题太难查，两边都收才能保证不漏。
        /// </summary>
        private List<Edge> CollectAllEdges()
        {
            var result = new List<Edge>();
            var seen = new HashSet<Edge>();

            foreach (var propertyEdge in edges)
            {
                if (propertyEdge != null && seen.Add(propertyEdge))
                {
                    result.Add(propertyEdge);
                }
            }

            foreach (var walkEdge in CollectAll<Edge>())
            {
                if (walkEdge != null && seen.Add(walkEdge))
                {
                    result.Add(walkEdge);
                }
            }

            return result;
        }

        /// <summary>图内右键菜单：在鼠标位置新建节点 / 分组框</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            // 屏幕坐标 → 图内容坐标
            Vector2 localPos = contentViewContainer.WorldToLocal(evt.mousePosition);

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("新建 对话 item", _ => CreateDialogueNode(localPos));
            evt.menu.AppendAction("新建 组头节点", _ => CreateGroupNode(localPos));
            evt.menu.AppendAction("新建 分组框", _ => CreateGroupBox(localPos));
        }

        // ===================== 复制 / 粘贴 =====================

        private const string ClipboardMarker = "DialogueGraphClipboard_v1";

        /// <summary>剪贴板数据（复制节点 + 复制范围内的连线）</summary>
        [System.Serializable]
        private class ClipboardPayload
        {
            public string marker;
            public List<DialogueGraphData.NodeData> nodes = new List<DialogueGraphData.NodeData>();
            public List<DialogueGraphData.LinkData> links = new List<DialogueGraphData.LinkData>();
        }

        private string SerializeForClipboard(IEnumerable<GraphElement> elements)
        {
            var payload = new ClipboardPayload { marker = ClipboardMarker };
            var copiedGuids = new HashSet<string>();

            foreach (var element in elements)
            {
                DialogueGraphData.NodeData nodeData = null;

                if (element is DialogueNodeView dialogueNode)
                {
                    nodeData = dialogueNode.ToData();
                }
                else if (element is GroupNodeView groupNode)
                {
                    nodeData = groupNode.ToData();
                }

                if (nodeData == null)
                {
                    continue;
                }

                copiedGuids.Add(nodeData.guid);
                payload.nodes.Add(nodeData);
            }

            if (payload.nodes.Count == 0)
            {
                return null;
            }

            // 只复制"两端都在复制范围内"的连线
            foreach (var edge in CollectAllEdges())
            {
                string fromGuid = GetGuid(edge.output != null ? edge.output.node : null);
                string toGuid = GetGuid(edge.input != null ? edge.input.node : null);

                if (fromGuid == null || toGuid == null) continue;
                if (!copiedGuids.Contains(fromGuid) || !copiedGuids.Contains(toGuid)) continue;

                payload.links.Add(new DialogueGraphData.LinkData
                {
                    fromGuid = fromGuid,
                    toGuid = toGuid,
                    fromPortName = edge.output != null ? edge.output.portName : null,
                });
            }

            return EditorJsonUtility.ToJson(payload);
        }

        private static bool CanPasteFromClipboard(string data)
        {
            return !string.IsNullOrEmpty(data);
        }

        private void PasteFromClipboard(string operationName, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            var payload = new ClipboardPayload();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(data, payload);
            }
            catch
            {
                return;
            }

            if (payload.nodes == null || payload.nodes.Count == 0)
            {
                return;
            }

            var oldToNewGuid = new Dictionary<string, string>();
            var newGuidToNode = new Dictionary<string, Node>();
            var offset = new Vector2(40f, 40f);

            foreach (var nodeData in payload.nodes)
            {
                string oldGuid = nodeData.guid;

                // 生成新 guid、位置偏移、不沿用原分组（粘贴位置变了）
                nodeData.guid = System.Guid.NewGuid().ToString();
                nodeData.position += offset;
                nodeData.groupTitle = null;

                oldToNewGuid[oldGuid] = nodeData.guid;

                if (nodeData.kind == DialogueGraphData.NodeKind.Dialogue)
                {
                    var view = CreateDialogueNode(nodeData.position);
                    view.SetData(nodeData);
                    newGuidToNode[nodeData.guid] = view;
                }
                else
                {
                    var view = CreateGroupNode(nodeData.position);
                    view.SetData(nodeData);
                    newGuidToNode[nodeData.guid] = view;
                }
            }

            // 用新 guid 重建复制范围内的连线
            if (payload.links != null)
            {
                foreach (var link in payload.links)
                {
                    if (!oldToNewGuid.TryGetValue(link.fromGuid, out var newFromGuid)) continue;
                    if (!oldToNewGuid.TryGetValue(link.toGuid, out var newToGuid)) continue;
                    if (!newGuidToNode.TryGetValue(newFromGuid, out var fromNode)) continue;
                    if (!newGuidToNode.TryGetValue(newToGuid, out var toNode)) continue;

                    var outputPort = GetOutputPort(fromNode, link.fromPortName);
                    var inputPort = GetInputPort(toNode);
                    if (outputPort == null || inputPort == null) continue;

                    AddElement(outputPort.ConnectTo(inputPort));
                }
            }

            // 选中刚粘贴出来的节点
            ClearSelection();
            foreach (var node in newGuidToNode.Values)
            {
                AddToSelection(node);
            }

            OnGraphMutated();
        }

        // ===================== 创建元素 =====================

        public DialogueNodeView CreateDialogueNode(Vector2 position)
        {
            var node = new DialogueNodeView();
            node.SetPosition(new Rect(position, new Vector2(280f, 220f)));
            node.OnChanged = OnNodeChanged;
            node.GroupTitleProvider = () => ResolveGroupTitle(node);   // 标题里带上组 id
            AddElement(node);
            node.RefreshTitle();
            OnGraphMutated();
            return node;
        }

        /// <summary>组头节点：承载分组的分镜模板与组音效，并作为组间连线的锚点</summary>
        public GroupNodeView CreateGroupNode(Vector2 position)
        {
            var node = new GroupNodeView();
            node.SetPosition(new Rect(position, new Vector2(260f, 150f)));
            node.OnChanged = OnNodeChanged;
            node.GroupTitleProvider = () => ResolveGroupTitle(node);   // 标题里带上组 id
            AddElement(node);
            node.RefreshTitle();
            OnGraphMutated();
            return node;
        }

        /// <summary>分组框：把节点拖进框里即视觉归组（GraphView 原生 Group 容器）</summary>
        public Group CreateGroupBox(Vector2 position)
        {
            var box = new Group
            {
                title = NextGroupTitle(),
            };
            box.SetPosition(new Rect(position, new Vector2(480f, 360f)));

            // 框加到最底层（先加框再加节点，保证框在节点下面）
            AddElement(box);
            box.SendToBack();
            OnGraphMutated();
            return box;
        }

        private string NextGroupTitle()
        {
            return $"Group_{CollectAll<Group>().Count() + 1}";
        }

        // ===================== 存 / 读 =====================

        /// <summary>把当前图收集进 SO 并落盘</summary>
        public void SaveToData(DialogueGraphData data)
        {
            if (data == null)
            {
                return;
            }

            Data = data;
            CollectInto(data);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            MarkSaved();

            // 这行日志是排查"存不进去"用的：图里的数量 vs 写进 SO 的数量
            int graphNodeCount = 0;
            foreach (var unused in CollectAll<Node>())
            {
                graphNodeCount++;
            }

            Debug.Log($"[对话图] 已保存：{data.name}｜图里 节点 {graphNodeCount} / 连线 {CollectAllEdges().Count}" +
                      $"｜写进 SO 节点 {data.nodes.Count} / 连线 {data.links.Count}");
        }

        /// <summary>只把图收集进 data，不落盘（撤销快照也走这里，所以不能碰 Data 引用）</summary>
        public void CollectInto(DialogueGraphData data)
        {
            if (data == null)
            {
                return;
            }

            data.nodes.Clear();
            data.links.Clear();
            data.groupBoxes.Clear();

            // 1) 分组框
            var boxes = CollectAll<Group>().ToList();
            foreach (var box in boxes)
            {
                var rect = box.GetPosition();
                data.groupBoxes.Add(new DialogueGraphData.GroupBoxData
                {
                    title = box.title,
                    position = rect.position,
                    size = rect.size,
                });
            }

            // 2) 节点（记录它落在哪个分组框里）
            foreach (var node in CollectAll<Node>())
            {
                DialogueGraphData.NodeData nodeData = null;

                if (node is DialogueNodeView dialogueNode)
                {
                    nodeData = dialogueNode.ToData();
                }
                else if (node is GroupNodeView groupNode)
                {
                    nodeData = groupNode.ToData();
                }

                if (nodeData == null)
                {
                    continue;
                }

                nodeData.groupTitle = FindGroupTitle(node, boxes);
                data.nodes.Add(nodeData);
            }

            // 3) 连线（记录输出端口名：组头有 out / nextGroup / choice_N 几个出口）
            var graphEdges = CollectAllEdges();
            int skippedEdges = 0;

            foreach (var edge in graphEdges)
            {
                string fromGuid = GetGuid(edge.output != null ? edge.output.node : null);
                string toGuid = GetGuid(edge.input != null ? edge.input.node : null);

                if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid))
                {
                    // 端点没有 guid（历史数据里出现过空 guid）→ 存下去也读不回来，直接跳过并报出来
                    skippedEdges++;
                    continue;
                }

                data.links.Add(new DialogueGraphData.LinkData
                {
                    fromGuid = fromGuid,
                    toGuid = toGuid,
                    fromPortName = edge.output != null ? edge.output.portName : null,
                });
            }

            if (skippedEdges > 0)
            {
                Debug.LogWarning($"[对话图] 有 {skippedEdges} 条连线因为端点缺少 guid 被跳过（那两端的节点需要重新建一次）");
            }
        }

        /// <summary>从 SO 载入整张图（用户主动加载 / 切换资产时用）</summary>
        public void LoadFromData(DialogueGraphData data)
        {
            Data = data;
            RebuildFrom(data);
            ResetHistory();
        }

        /// <summary>按数据重建图（不动 Data 引用：撤销/重做也走这里）</summary>
        public void RebuildFrom(DialogueGraphData data)
        {
            // 清空现有图（含被装进分组框里的节点）
            DeleteElements(CollectAll<GraphElement>().ToList());

            if (data == null)
            {
                return;
            }

            // 1) 先建分组框（框要在节点下面）
            var titleToBox = new Dictionary<string, Group>();
            foreach (var boxData in data.groupBoxes)
            {
                var box = CreateGroupBox(boxData.position);
                box.title = boxData.title;
                box.SetPosition(new Rect(boxData.position, boxData.size));
                titleToBox[boxData.title] = box;
            }

            // 2) 建节点，并按 groupTitle 归入分组框
            var guidToNode = new Dictionary<string, Node>();
            foreach (var nodeData in data.nodes)
            {
                if (nodeData.kind == DialogueGraphData.NodeKind.Dialogue)
                {
                    var view = CreateDialogueNode(nodeData.position);
                    view.SetData(nodeData);
                    guidToNode[nodeData.guid] = view;

                    if (!string.IsNullOrEmpty(nodeData.groupTitle) &&
                        titleToBox.TryGetValue(nodeData.groupTitle, out var box))
                    {
                        box.AddElement(view);
                    }
                }
                else
                {
                    var view = CreateGroupNode(nodeData.position);
                    view.SetData(nodeData);
                    guidToNode[nodeData.guid] = view;

                    if (!string.IsNullOrEmpty(nodeData.groupTitle) &&
                        titleToBox.TryGetValue(nodeData.groupTitle, out var box))
                    {
                        box.AddElement(view);
                    }
                }
            }

            // 3) 建连线
            int brokenLinks = 0;

            foreach (var link in data.links)
            {
                if (!guidToNode.TryGetValue(link.fromGuid, out var fromNode) ||
                    !guidToNode.TryGetValue(link.toGuid, out var toNode))
                {
                    // 端点 guid 对不上（典型情况：旧数据里节点 guid 是空的）
                    brokenLinks++;
                    continue;
                }

                var outputPort = GetOutputPort(fromNode, link.fromPortName);
                var inputPort = GetInputPort(toNode);

                if (outputPort == null || inputPort == null)
                {
                    brokenLinks++;
                    continue;
                }

                AddElement(outputPort.ConnectTo(inputPort));
            }

            if (brokenLinks > 0)
            {
                Debug.LogWarning($"[对话图] 有 {brokenLinks} 条连线读不回来（端点 guid 对不上或端口不存在），" +
                                 "多半是旧数据里节点 guid 为空导致的；重新连一次就正常了");
            }

            // 建完统一刷一次标题（这时候组 id 已经能算出来了）
            RefreshAllTitles();
        }

        // ===================== 工具 =====================

        /// <summary>节点属于哪个组（= 它所在分组框的标题；不在任何框里返回 null）</summary>
        public string ResolveGroupTitle(Node node)
        {
            if (node == null)
            {
                return null;
            }

            return FindGroupTitle(node, CollectAll<Group>().ToList());
        }

        /// <summary>
        /// 重刷所有节点标题。
        /// 标题里带组 id，而"组"是框的包含关系表达出来的 —— 节点拖进/拖出框、框改名、
        /// 撤销重做之后，归属都可能变，所以这些时机都要重刷一次。
        /// </summary>
        public void RefreshAllTitles()
        {
            foreach (var node in CollectAll<Node>())
            {
                if (node is DialogueNodeView dialogue)
                {
                    dialogue.RefreshTitle();
                }
                else if (node is GroupNodeView header)
                {
                    header.RefreshTitle();
                }
            }
        }

        /// <summary>节点落在哪个分组框里（不在任何框内返回 null）</summary>
        private static string FindGroupTitle(Node node, List<Group> boxes)
        {
            foreach (var box in boxes)
            {
                if (IsInBox(node, box))
                {
                    return box.title;
                }
            }

            return null;
        }

        /// <summary>
        /// 节点是否属于这个框：
        ///   ① 已经是框的子元素（AddElement 进去的，最可靠，也不受坐标换算影响）
        ///   ② 视觉上节点的中心落在框里（用户直接拖进去的情况）
        /// </summary>
        private static bool IsInBox(Node node, Group box)
        {
            for (VisualElement parent = node.parent; parent != null; parent = parent.parent)
            {
                if (parent == box)
                {
                    return true;
                }
            }

            return box.GetPosition().Contains(node.GetPosition().center);
        }

        private static string GetGuid(Node node)
        {
            if (node is DialogueNodeView dialogueNode) return dialogueNode.GUID;
            if (node is GroupNodeView groupNode) return groupNode.GUID;
            return null;
        }

        /// <summary>
        /// 按端口名取输出端口：
        ///   组头的 out（组内第一句）/ nextGroup（线性下一组）/ choice_N（第 N 个选项）
        ///   对话节点只有 out
        /// </summary>
        private static Port GetOutputPort(Node node, string portName)
        {
            if (node is DialogueNodeView dialogueNode)
            {
                return dialogueNode.OutputPort;
            }

            if (node is GroupNodeView groupNode)
            {
                // 选项端口：端口名里的数字就是选项下标
                if (!string.IsNullOrEmpty(portName) && portName.StartsWith(GroupNodeView.ChoicePortPrefix))
                {
                    string indexText = portName.Substring(GroupNodeView.ChoicePortPrefix.Length);
                    return int.TryParse(indexText, out int choiceIndex) ? groupNode.GetChoicePort(choiceIndex) : null;
                }

                // 兼容旧数据里保存的 "next"
                bool isNextGroup = portName == "nextGroup" || portName == "next";
                return isNextGroup ? groupNode.NextPort : groupNode.OutputPort;
            }

            return null;
        }

        private static Port GetInputPort(Node node)
        {
            if (node is DialogueNodeView dialogueNode) return dialogueNode.InputPort;
            if (node is GroupNodeView groupNode) return groupNode.InputPort;   // 组间入口
            return null;
        }

        // ===================== 导出：图 → DialogueChapterSO =====================

        /// <summary>
        /// 把图摊平成运行时用的 DialogueChapterSO（分叉会写成 choices + targetGroupId），
        /// 同时产出一份检查报告（窗口底部面板显示它）。
        /// 规则：
        ///   1. 一个分组框 = 一个组，框标题就是 groupId（空/重名 → 报错）
        ///   2. 组头 out 口顺着连 = 组内台词顺序；没连线的组按 y 坐标从上到下兜底
        ///   3. choice_i 口 = 第 i 个选项跳到哪一组；nextGroup 口 = 线性下一组（没连 = 本章结束）
        ///   4. 起始组：图数据里手填的 startGroupId 优先，否则自动推断（没有任何外来线连进来那个）
        /// dryRun = true 时只校验 + 出预览，绝不写资产（窗口里的"检查"按钮走这条）。
        /// </summary>
        public GraphCheckReport ExportToChapter(DialogueChapterSO chapter, bool dryRun = false)
        {
            var report = new GraphCheckReport();

            // 组 id → 组头节点 guid：面板里点一条问题就跳到对应组
            var headerGuidByGroupId = new Dictionary<string, string>();

            if (chapter == null && !dryRun)
            {
                Debug.LogError("[对话图] 导出失败：没有指定 DialogueChapterSO 资产");
                return report;
            }

            var errors = new List<GraphIssue>();
            var warnings = new List<GraphIssue>();

            // ---- 0) 分组框：框标题 = 组 id ----
            var boxes = CollectAll<Group>().ToList();
            foreach (var box in boxes)
            {
                if (string.IsNullOrWhiteSpace(box.title))
                {
                    errors.Add("有分组框没有标题（框标题就是组 id，必须填）");
                }
            }

            foreach (var duplicate in boxes.GroupBy(b => b.title).Where(g => g.Count() > 1))
            {
                errors.Add($"分组框标题重复：{duplicate.Key}（组 id 必须唯一）");
            }

            // ---- 1) 组头节点 → 组 ----
            var headerByGroupId = new Dictionary<string, GroupNodeView>();
            var groupIdByHeader = new Dictionary<GroupNodeView, string>();

            foreach (var node in CollectAll<Node>())
            {
                if (!(node is GroupNodeView header))
                {
                    continue;
                }

                string groupId = FindGroupTitle(header, boxes);
                if (string.IsNullOrEmpty(groupId))
                {
                    errors.Add("有组头节点不在任何分组框里（把组头拖进框里）");
                    continue;
                }

                if (headerByGroupId.ContainsKey(groupId))
                {
                    errors.Add($"框「{groupId}」里有多个组头节点，只能有一个");
                    continue;
                }

                headerByGroupId.Add(groupId, header);
                groupIdByHeader.Add(header, groupId);
                headerGuidByGroupId[groupId] = header.GUID;
            }

            foreach (var box in boxes)
            {
                if (!string.IsNullOrEmpty(box.title) && !headerByGroupId.ContainsKey(box.title))
                {
                    errors.Add($"框「{box.title}」里没有组头节点");
                }
            }

            if (headerByGroupId.Count == 0)
            {
                errors.Add("图里一个有效的组都没有");
            }

            if (errors.Count > 0)
            {
                ReportExportResult(report, errors, warnings, false, dryRun, headerGuidByGroupId);
                return report;
            }

            // ---- 2) 每个组：组内台词 + 出口 ----
            var groupDataById = new Dictionary<string, DialogueShowGroupData>();
            var usedDialogueNodes = new HashSet<DialogueNodeView>();

            foreach (var pair in headerByGroupId)
            {
                string groupId = pair.Key;
                var header = pair.Value;

                var groupData = new DialogueShowGroupData
                {
                    groupId = groupId,
                };

                // 组头的固定字段（分镜模板 / 组音效）走字段表：加一个组级字段不用回来改这里
                var fieldWarnings = new List<string>();
                DialogueNodeFields.ApplyToGroupData(header, groupData, fieldWarnings);

                // 字段表不知道自己在哪个组里，这里补上组名，报告里才定位得到
                foreach (var message in fieldWarnings)
                {
                    warnings.Add($"组「{groupId}」的{message}");
                }

                // 组内台词顺序：优先跟 out 口的连线走
                var chain = new List<DialogueNodeView>();
                var seenInChain = new HashSet<DialogueNodeView>();
                var cursor = GetFirstConnectedNode(header.OutputPort) as DialogueNodeView;

                // 从组内某句连到"别的组的台词"时，那条线就当"跳到那个组"用
                // （策划很自然会这么连，不用非得连到组头的 in 口）
                DialogueNodeView exitNode = null;

                while (cursor != null)
                {
                    string nodeGroupId = FindGroupTitle(cursor, boxes);

                    if (string.IsNullOrEmpty(nodeGroupId))
                    {
                        // 不在任何框里：宽容处理，按当前组继续，但要说一声
                        warnings.Add($"组「{groupId}」的台词「{Preview(cursor.GetText("text"))}」不在任何分组框里，先按本组算（建议拖进框）");
                    }
                    else if (nodeGroupId != groupId)
                    {
                        // 跨框了 = 跳到另一组，本组台词到此为止
                        exitNode = cursor;
                        break;
                    }

                    if (!seenInChain.Add(cursor))
                    {
                        errors.Add($"组「{groupId}」的台词连成环了（检查 out 口连线）");
                        break;
                    }

                    if (!usedDialogueNodes.Add(cursor))
                    {
                        errors.Add($"台词被两个组共用了：「{Preview(cursor.GetText("text"))}」");
                        break;
                    }

                    chain.Add(cursor);
                    cursor = GetFirstConnectedNode(cursor.OutputPort) as DialogueNodeView;
                }

                // 组头 out 完全没连线：按 y 坐标从上到下兜底（策划可以少画一堆线）
                if (chain.Count == 0 && GetConnections(header.OutputPort).Count == 0)
                {
                    var inBox = CollectDialogueNodesInBox(boxes, groupId);
                    inBox.Sort((a, b) => GetContentPosition(a).y.CompareTo(GetContentPosition(b).y));

                    foreach (var node in inBox)
                    {
                        if (usedDialogueNodes.Add(node))
                        {
                            chain.Add(node);
                        }
                    }

                    if (chain.Count > 0)
                    {
                        warnings.Add($"组「{groupId}」的组头 out 没连线，按 y 坐标从上到下排了 {chain.Count} 句");
                    }
                }

                foreach (var node in chain)
                {
                    // 字段映射走 DialogueNodeFields 这张表：加一个台词字段不用回来改这里
                    var item = new DialogueShowTextItem();
                    DialogueNodeFields.ApplyToShowItem(node, item);
                    groupData.groupTexts.Add(item);
                }

                // 选项（分叉）：choice_i 口连到哪一组
                for (int i = 0; i < header.ChoiceCount; i++)
                {
                    string choiceText = header.GetChoiceText(i);
                    string targetGroupId = ResolveGroupId(
                        GetFirstConnectedNode(header.GetChoicePort(i)), groupIdByHeader, boxes);

                    if (string.IsNullOrEmpty(targetGroupId))
                    {
                        warnings.Add($"组「{groupId}」的选项 {i}（{choiceText}）没连目标组，已跳过");
                        continue;
                    }

                    groupData.choices.Add(new DialogueChoiceData
                    {
                        text = choiceText,
                        targetGroupId = targetGroupId,
                    });
                }

                // 线性下一组
                var nextEdges = GetConnections(header.NextPort);
                if (nextEdges.Count > 1)
                {
                    warnings.Add($"组「{groupId}」的 nextGroup 连了 {nextEdges.Count} 条，只取第一条（要分叉请用选项端口）");
                }

                if (nextEdges.Count > 0)
                {
                    var nextNode = nextEdges[0].input != null ? nextEdges[0].input.node : null;
                    groupData.nextGroupId = ResolveGroupId(nextNode, groupIdByHeader, boxes);

                    if (string.IsNullOrEmpty(groupData.nextGroupId))
                    {
                        warnings.Add($"组「{groupId}」的 nextGroup 连到的节点不属于任何组，当作本章结束");
                    }
                }
                else if (exitNode != null)
                {
                    // 组内最后一句直接连到了别的组的台词 → 那就是下一组
                    groupData.nextGroupId = FindGroupTitle(exitNode, boxes);

                    if (string.IsNullOrEmpty(groupData.nextGroupId))
                    {
                        warnings.Add($"组「{groupId}」的台词连到了一个不属于任何组的节点，当作本章结束");
                    }
                }

                if (groupData.HasChoice && !string.IsNullOrEmpty(groupData.nextGroupId))
                {
                    warnings.Add($"组「{groupId}」同时有选项和 nextGroup：运行时以选项为准，nextGroup 不生效");
                }

                groupDataById.Add(groupId, groupData);
            }

            if (errors.Count > 0)
            {
                ReportExportResult(report, errors, warnings, false, dryRun, headerGuidByGroupId);
                return report;
            }

            // ---- 3) 起始组：没有任何"从别的组跳进来"的线 ----
            var hasIncoming = new HashSet<string>();
            foreach (var edge in CollectAllEdges())
            {
                var fromNode = edge.output != null ? edge.output.node : null;
                var toNode = edge.input != null ? edge.input.node : null;

                string fromGroupId = ResolveGroupId(fromNode, groupIdByHeader, boxes);
                string toGroupId = ResolveGroupId(toNode, groupIdByHeader, boxes);

                // 组内的台词连线不算"从外面跳进来"
                if (!string.IsNullOrEmpty(toGroupId) && fromGroupId != toGroupId)
                {
                    hasIncoming.Add(toGroupId);
                }
            }

            var startCandidates = headerByGroupId.Keys.Where(id => !hasIncoming.Contains(id)).ToList();

            // 手填的起始组优先（窗口工具栏里那个"起始组"框，存在图数据的 startGroupId）
            string manualStart = Data != null ? Data.startGroupId : null;
            string startGroupId = null;

            if (!string.IsNullOrEmpty(manualStart) && headerByGroupId.ContainsKey(manualStart))
            {
                startGroupId = manualStart;
            }
            else
            {
                if (!string.IsNullOrEmpty(manualStart))
                {
                    warnings.Add($"指定的起始组「{manualStart}」在图里不存在，已改回自动推断");
                }

                if (startCandidates.Count == 1)
                {
                    startGroupId = startCandidates[0];
                }
                else if (startCandidates.Count == 0)
                {
                    errors.Add("每个组都有线跳进来，找不到起始组（第一个组应该没有入口连线）");
                }
                else
                {
                    startGroupId = startCandidates[0];
                    warnings.Add($"有 {startCandidates.Count} 个组没人连进来（{string.Join("、", startCandidates)}），" +
                                 $"把「{startGroupId}」当起始组");
                }
            }

            if (errors.Count > 0 || string.IsNullOrEmpty(startGroupId))
            {
                ReportExportResult(report, errors, warnings, false, dryRun, headerGuidByGroupId);
                return report;
            }

            // ---- 4) 写入顺序：从起始组出发，按 线性 → 选项 广度优先 ----
            //      单纯为了在 Inspector 里能按剧情顺序看；运行时不依赖顺序（跳转全靠 groupId）
            var orderedIds = new List<string>();
            var pending = new Queue<string>();
            var enqueued = new HashSet<string> { startGroupId };
            pending.Enqueue(startGroupId);

            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                orderedIds.Add(id);

                var groupData = groupDataById[id];

                if (!string.IsNullOrEmpty(groupData.nextGroupId) && enqueued.Add(groupData.nextGroupId))
                {
                    pending.Enqueue(groupData.nextGroupId);
                }

                foreach (var choice in groupData.choices)
                {
                    if (!string.IsNullOrEmpty(choice.targetGroupId) && enqueued.Add(choice.targetGroupId))
                    {
                        pending.Enqueue(choice.targetGroupId);
                    }
                }
            }

            var unreachable = groupDataById.Keys.Where(id => !enqueued.Contains(id)).ToList();
            if (unreachable.Count > 0)
            {
                warnings.Add($"这些组没有任何线能走到：{string.Join("、", unreachable)}");
                orderedIds.AddRange(unreachable);
            }

            // ---- 4.5) 填面板预览（不动资产，纯给"检查"面板看）----
            report.StartGroupId = startGroupId;

            foreach (var id in orderedIds)
            {
                var groupData = groupDataById[id];
                var preview = new GroupPreviewItem
                {
                    GroupId = id,
                    IsStart = id == startGroupId,
                    TextCount = groupData.TextCount,
                    FirstLine = groupData.TextCount > 0 ? Preview(groupData.groupTexts[0].text) : "（空组）",
                    NextGroupId = groupData.nextGroupId,
                };

                if (headerGuidByGroupId.TryGetValue(id, out var headerGuid))
                {
                    preview.HeaderGuid = headerGuid;
                }

                foreach (var choice in groupData.choices)
                {
                    string choiceText = string.IsNullOrEmpty(choice.text) ? "（没填选项文本）" : choice.text;
                    preview.ChoiceLines.Add($"{choiceText} → {choice.targetGroupId}");
                }

                report.Groups.Add(preview);
            }

            // ---- 5) 写资产（dryRun = 只检查，什么都没写）----
            if (!dryRun)
            {
                chapter.startGroupId = startGroupId;
                chapter.groups = orderedIds.Select(id => groupDataById[id]).ToList();

                EditorUtility.SetDirty(chapter);
                AssetDatabase.SaveAssets();
            }

            ReportExportResult(report, errors, warnings, true, dryRun, headerGuidByGroupId);
            return report;
        }

        // ===================== 导出用的小工具 =====================

        private static List<Edge> GetConnections(Port port)
        {
            return port == null ? new List<Edge>() : port.connections.ToList();
        }

        /// <summary>输出端口连到的那个节点（没连返回 null）</summary>
        private static Node GetFirstConnectedNode(Port port)
        {
            if (port == null)
            {
                return null;
            }

            foreach (var edge in port.connections)
            {
                var other = port.direction == Direction.Output ? edge.input : edge.output;
                if (other != null)
                {
                    return other.node;
                }
            }

            return null;
        }

        /// <summary>对端节点属于哪一组：组头就是它自己；台词节点看它落在哪个框里</summary>
        private static string ResolveGroupId(Node node, Dictionary<GroupNodeView, string> groupIdByHeader, List<Group> boxes)
        {
            if (node is GroupNodeView header && groupIdByHeader.TryGetValue(header, out var id))
            {
                return id;
            }

            if (node is DialogueNodeView dialogueNode)
            {
                return FindGroupTitle(dialogueNode, boxes);
            }

            return null;
        }

        /// <summary>某个组（框）里的台词节点</summary>
        private List<DialogueNodeView> CollectDialogueNodesInBox(List<Group> boxes, string groupId)
        {
            var result = new List<DialogueNodeView>();
            Group box = boxes.FirstOrDefault(b => b.title == groupId);

            if (box == null)
            {
                return result;
            }

            foreach (var node in CollectAll<Node>())
            {
                if (node is DialogueNodeView dialogueNode && IsInBox(dialogueNode, box))
                {
                    result.Add(dialogueNode);
                }
            }

            return result;
        }

        /// <summary>报错信息里显示台词片段</summary>
        private static string Preview(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "（空台词）";
            }

            return text.Length <= 12 ? text : text.Substring(0, 12) + "…";
        }

        /// <summary>
        /// 校验结果落到 report（窗口底部面板读它），并往 Console 过一遍。
        /// 只有"真的导出"且失败时才弹窗——"检查"按钮不该弹模态框。
        /// </summary>
        private void ReportExportResult(GraphCheckReport report, List<GraphIssue> errors,
            List<GraphIssue> warnings, bool success, bool dryRun,
            Dictionary<string, string> headerGuidByGroupId)
        {
            report.IsSuccess = success;
            report.Issues.Clear();

            foreach (var error in errors)
            {
                error.IsError = true;
                report.Issues.Add(error);
            }

            foreach (var warning in warnings)
            {
                warning.IsError = false;
                report.Issues.Add(warning);
            }

            // 消息里带了组 id 的（都是「组id」这种写法），直接定位到那个组头，面板里点一下跳过去
            foreach (var issue in report.Issues)
            {
                if (!string.IsNullOrEmpty(issue.NodeGuid) || string.IsNullOrEmpty(issue.Message))
                {
                    continue;
                }

                foreach (var pair in headerGuidByGroupId)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && issue.Message.Contains($"「{pair.Key}」"))
                    {
                        issue.NodeGuid = pair.Value;
                        break;
                    }
                }
            }

            foreach (var issue in report.Issues)
            {
                if (issue.IsError)
                {
                    Debug.LogError($"[对话图] {issue.Message}");
                }
                else
                {
                    Debug.LogWarning($"[对话图] {issue.Message}");
                }
            }

            LastReport = report;

            if (success)
            {
                Debug.Log($"[对话图] {(dryRun ? "检查通过" : "导出完成")}：组 {report.Groups.Count} 个，" +
                          $"选项 {report.TotalChoiceCount} 个，起始组「{report.StartGroupId}」" +
                          (report.WarningCount > 0 ? $"，警告 {report.WarningCount} 条（看 Console）" : ""));
                return;
            }

            if (dryRun)
            {
                return;   // 只检查：面板上已经列出来了，不弹窗
            }

            EditorUtility.DisplayDialog("导出失败",
                "先把这些修了：\n\n" + string.Join("\n", errors.Select(e => e.Message)) +
                (warnings.Count > 0 ? $"\n\n（另有 {warnings.Count} 条警告，看 Console）" : ""),
                "好");
        }
    }
}
