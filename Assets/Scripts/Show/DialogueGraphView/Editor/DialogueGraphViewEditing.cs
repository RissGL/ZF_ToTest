using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话图编辑器：编辑相关的部分（撤销/重做、脏标记、新建位置、定位、自动串联、装框）。
    /// 用 partial 跟 DialogueGraphView.cs 拆开，免得一个文件上千行。
    ///
    /// 撤销的实现是"快照栈"：
    ///   m_CurrentSnapshot = 现在屏幕上的图（JSON）
    ///   m_SavedSnapshot   = 上次存进 SO 的图 → 两者不同就是"脏"
    ///   每次图变了就把"变动前"的状态压进 m_UndoStack
    /// 不用 Unity 的 Undo 系统，是因为 GraphView 的增删连线不走 SerializedObject，
    /// 挂不上官方 Undo；快照法对"连线/拖动/改字段"一律有效。
    /// </summary>
    public partial class DialogueGraphView
    {
        private const int MaxUndoSteps = 60;

        private readonly List<string> m_UndoStack = new List<string>();
        private readonly List<string> m_RedoStack = new List<string>();

        private string m_CurrentSnapshot = string.Empty;
        private string m_SavedSnapshot = string.Empty;

        /// <summary>正在恢复快照：期间的图变动不要再记一笔</summary>
        private bool m_Restoring;

        /// <summary>只用来序列化快照，不落盘、不进 Data</summary>
        private DialogueGraphData m_SnapshotBuffer;

        /// <summary>连续新建节点时的错位量，免得全叠在一个点上</summary>
        private Vector2 m_SpawnCascade;

        /// <summary>上次记快照的时间：短时间内的连续改动（新建+摆位置、拖动+连线）合并成一步撤销</summary>
        private double m_LastSnapshotTime;

        /// <summary>上次"检查"的结果（窗口底部的面板读它）</summary>
        public GraphCheckReport LastReport { get; private set; }

        public bool IsDirty => m_CurrentSnapshot != m_SavedSnapshot;
        public bool CanUndo => m_UndoStack.Count > 0;
        public bool CanRedo => m_RedoStack.Count > 0;

        // ===================== 快照 / 撤销 =====================

        private DialogueGraphData GetSnapshotBuffer()
        {
            if (m_SnapshotBuffer == null)
            {
                m_SnapshotBuffer = ScriptableObject.CreateInstance<DialogueGraphData>();
                m_SnapshotBuffer.hideFlags = HideFlags.HideAndDontSave;
            }

            return m_SnapshotBuffer;
        }

        /// <summary>窗口关掉时记得清掉临时 SO，别留在内存里</summary>
        public void DisposeSnapshotBuffer()
        {
            if (m_SnapshotBuffer != null)
            {
                Object.DestroyImmediate(m_SnapshotBuffer);
                m_SnapshotBuffer = null;
            }
        }

        private string CaptureSnapshot()
        {
            CollectInto(GetSnapshotBuffer());
            return JsonUtility.ToJson(GetSnapshotBuffer());
        }

        /// <summary>
        /// 图变了：记脏 + 压撤销栈。
        /// 内容跟上次一样就什么都不做（GraphView 会为"选中/空点击"也发事件，去重很有必要）。
        /// 节点视图和 GraphView 回调都调这里。
        /// </summary>
        public void OnGraphMutated()
        {
            if (m_Restoring)
            {
                return;
            }

            string now = CaptureSnapshot();
            if (now == m_CurrentSnapshot)
            {
                return;
            }

            // 距上次记录很近 = 同一次操作的一部分（新建完再摆位置、拖完再连线）：
            // 不再压栈，这样一次 Ctrl+Z 能把整串操作一起撤掉
            double time = EditorApplication.timeSinceStartup;
            if (m_UndoStack.Count == 0 || time - m_LastSnapshotTime > 0.25d)
            {
                m_UndoStack.Add(m_CurrentSnapshot);
                if (m_UndoStack.Count > MaxUndoSteps)
                {
                    m_UndoStack.RemoveAt(0);
                }
            }

            m_LastSnapshotTime = time;

            m_RedoStack.Clear();
            m_CurrentSnapshot = now;

            // 组归属可能变了（节点挪进/挪出框、框改名），标题跟着重刷
            RefreshAllTitles();
        }

        /// <summary>重新加载 / 首次载入后调用：历史清空，当前状态 = 已保存状态</summary>
        public void ResetHistory()
        {
            m_UndoStack.Clear();
            m_RedoStack.Clear();
            m_CurrentSnapshot = CaptureSnapshot();
            m_SavedSnapshot = m_CurrentSnapshot;
        }

        /// <summary>存过 SO 之后调用</summary>
        public void MarkSaved()
        {
            m_SavedSnapshot = m_CurrentSnapshot;
        }

        public void Undo()
        {
            if (m_UndoStack.Count == 0)
            {
                return;
            }

            string target = m_UndoStack[m_UndoStack.Count - 1];
            m_UndoStack.RemoveAt(m_UndoStack.Count - 1);
            m_RedoStack.Add(m_CurrentSnapshot);
            RestoreSnapshot(target);
        }

        public void Redo()
        {
            if (m_RedoStack.Count == 0)
            {
                return;
            }

            string target = m_RedoStack[m_RedoStack.Count - 1];
            m_RedoStack.RemoveAt(m_RedoStack.Count - 1);
            m_UndoStack.Add(m_CurrentSnapshot);
            RestoreSnapshot(target);
        }

        private void RestoreSnapshot(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            m_Restoring = true;

            try
            {
                JsonUtility.FromJsonOverwrite(json, GetSnapshotBuffer());
                RebuildFrom(GetSnapshotBuffer());     // 注意：不动 Data 引用，SO 资产不受影响
                m_CurrentSnapshot = json;
                LastReport = null;                    // 图变了，上次的检查结果作废
            }
            finally
            {
                m_Restoring = false;
            }
        }

        /// <summary>
        /// GraphView 的删除 / 连线 / 拖动会走这里。
        /// 注意 2022 的 GraphViewChange 只有 elementsToRemove / edgesToCreate / movedElements / moveDelta：
        ///   删掉的连线是混在 elementsToRemove 里的；
        ///   而"新建元素"根本不会回调，所以新建的地方（CreateXxx / 粘贴）自己调 OnGraphMutated。
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            bool mutated =
                (change.elementsToRemove != null && change.elementsToRemove.Count > 0) ||
                (change.edgesToCreate != null && change.edgesToCreate.Count > 0) ||
                (change.movedElements != null && change.movedElements.Count > 0);

            if (mutated)
            {
                OnGraphMutated();
            }

            return change;   // 不拦截改动，原样交回给 GraphView
        }

        /// <summary>节点里的输入框/资产槽变了（节点视图回调这个）</summary>
        private void OnNodeChanged()
        {
            OnGraphMutated();
        }

        // ===================== 新建位置 =====================

        /// <summary>新节点放哪：优先当前视图中心，连着建就错开，避免叠在一起</summary>
        public Vector2 GetSpawnPosition()
        {
            Vector2 position;

            Rect bound = worldBound;
            if (bound.width > 1f && bound.height > 1f)
            {
                // worldBound 是面板坐标，换算到图内容坐标（缩放/平移都算进去了）
                position = contentViewContainer.WorldToLocal(bound.center) - new Vector2(150f, 130f);
            }
            else
            {
                position = new Vector2(60f, 60f);
            }

            position += m_SpawnCascade;

            m_SpawnCascade += new Vector2(36f, 36f);
            if (m_SpawnCascade.x > 240f || m_SpawnCascade.y > 240f)
            {
                m_SpawnCascade = Vector2.zero;
            }

            return position;
        }

        /// <summary>
        /// 节点在图内容坐标系里的位置。
        /// 被 AddElement 进分组框的节点，GetPosition() 会变成相对框的坐标，排序/算包围盒会错，所以统一走这里。
        /// </summary>
        private Vector2 GetContentPosition(Node node)
        {
            Rect world = node.worldBound;
            if (world.width > 1f && world.height > 1f)
            {
                return contentViewContainer.WorldToLocal(world.position);
            }

            return node.GetPosition().position;
        }

        // ===================== 定位 =====================

        /// <summary>按 guid 选中并聚焦某个节点（检查面板点一下跳过去）</summary>
        public void FocusNode(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            foreach (var node in CollectAll<Node>())
            {
                if (GetGuid(node) == guid)
                {
                    ClearSelection();
                    AddToSelection(node);
                    FrameSelection();
                    return;
                }
            }
        }

        /// <summary>把起始组的组头标出来（标题变绿 + 加个星 + 显示组 id）</summary>
        public void MarkStartGroup(string startGroupId)
        {
            var boxes = CollectAll<Group>().ToList();

            foreach (var node in CollectAll<Node>())
            {
                if (!(node is GroupNodeView header))
                {
                    continue;
                }

                bool isStart = !string.IsNullOrEmpty(startGroupId) &&
                               FindGroupTitle(header, boxes) == startGroupId;

                header.SetStartGroup(isStart);

                var label = header.titleContainer.Q<Label>();
                if (label != null)
                {
                    label.style.color = isStart
                        ? new Color(0.45f, 1f, 0.45f)
                        : new Color(1f, 0.85f, 0.4f);
                }
            }

            // 其他节点的标题也可能还没算过组 id（比如刚打开图）
            RefreshAllTitles();
        }

        // ===================== 批量编辑 =====================

        /// <summary>选中节点里的组头（选取中的第一个组头）</summary>
        public GroupNodeView GetSelectedGroupHeader()
        {
            foreach (var selectable in selection)
            {
                if (selectable is GroupNodeView header)
                {
                    return header;
                }
            }

            // 选中的是台词节点：用它所在框里的组头
            var boxes = CollectAll<Group>().ToList();
            foreach (var selectable in selection)
            {
                if (!(selectable is DialogueNodeView line))
                {
                    continue;
                }

                string groupId = FindGroupTitle(line, boxes);
                if (string.IsNullOrEmpty(groupId))
                {
                    continue;
                }

                foreach (var node in CollectAll<Node>())
                {
                    if (node is GroupNodeView candidate && FindGroupTitle(candidate, boxes) == groupId)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 把某一组里的台词按 y 坐标从上到下自动串起来：组头 out → 第 1 句 → … → 最后一句。
        /// 只断"组内台词之间"的连线，组间跳转线（nextGroup / 选项 / 跨框）一律不动。
        /// </summary>
        public bool AutoChainGroup(GroupNodeView header, out string message)
        {
            message = null;

            if (header == null)
            {
                message = "先选中一个组头节点（或者选中组里任意一句台词）";
                return false;
            }

            var boxes = CollectAll<Group>().ToList();
            string groupId = FindGroupTitle(header, boxes);

            if (string.IsNullOrEmpty(groupId))
            {
                message = "这个组头不在任何分组框里，先把它拖进框";
                return false;
            }

            var lines = CollectDialogueNodesInBox(boxes, groupId);
            lines.Sort((a, b) => GetContentPosition(a).y.CompareTo(GetContentPosition(b).y));

            if (lines.Count == 0)
            {
                message = $"框「{groupId}」里没有台词节点";
                return false;
            }

            var inner = new HashSet<DialogueNodeView>(lines);

            // 1) 先断线：组头 out 上的、以及组内台词互相之间的
            foreach (var edge in GetConnections(header.OutputPort).ToList())
            {
                DisconnectEdge(edge);
            }

            foreach (var line in lines)
            {
                foreach (var edge in GetConnections(line.InputPort).Concat(GetConnections(line.OutputPort)).ToList())
                {
                    var fromNode = edge.output != null ? edge.output.node : null;
                    var toNode = edge.input != null ? edge.input.node : null;

                    bool bothInside = fromNode is DialogueNodeView f && inner.Contains(f) &&
                                      toNode is DialogueNodeView t && inner.Contains(t);

                    if (bothInside || fromNode == header || toNode == header)
                    {
                        DisconnectEdge(edge);
                    }
                }
            }

            // 2) 重新串
            ConnectPorts(header.OutputPort, lines[0].InputPort);
            for (int i = 0; i < lines.Count - 1; i++)
            {
                ConnectPorts(lines[i].OutputPort, lines[i + 1].InputPort);
            }

            OnGraphMutated();
            message = $"组「{groupId}」已按 y 坐标串好 {lines.Count} 句";
            return true;
        }

        /// <summary>把选中的节点装进一个新分组框（Ctrl+G）</summary>
        public bool GroupSelectionIntoNewBox(out string message)
        {
            message = null;

            var selected = new List<Node>();
            foreach (var selectable in selection)
            {
                if (selectable is GroupNodeView header)
                {
                    selected.Add(header);
                }
                else if (selectable is DialogueNodeView line)
                {
                    selected.Add(line);
                }
            }

            if (selected.Count == 0)
            {
                message = "先在图上选中要装框的节点";
                return false;
            }

            Rect bounds = new Rect(GetContentPosition(selected[0]), selected[0].GetPosition().size);
            for (int i = 1; i < selected.Count; i++)
            {
                var rect = new Rect(GetContentPosition(selected[i]), selected[i].GetPosition().size);
                bounds = Union(bounds, rect);
            }

            // 上面留出框头的位置，四周留 padding
            bounds = new Rect(bounds.x - 30f, bounds.y - 50f, bounds.width + 60f, bounds.height + 80f);

            var box = CreateGroupBox(bounds.position);
            box.SetPosition(bounds);

            foreach (var node in selected)
            {
                box.AddElement(node);
            }

            OnGraphMutated();
            message = $"已把 {selected.Count} 个节点装进「{box.title}」";
            return true;
        }

        /// <summary>复制选中节点（Ctrl+D）</summary>
        public void DuplicateSelection()
        {
            var elements = new List<GraphElement>();
            foreach (var selectable in selection)
            {
                if (selectable is GraphElement element)
                {
                    elements.Add(element);
                }
            }

            if (elements.Count == 0)
            {
                return;
            }

            string json = SerializeForClipboard(elements);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            PasteFromClipboard("Duplicate", json);
            OnGraphMutated();
        }

        // ===================== 小工具 =====================

        private void DisconnectEdge(Edge edge)
        {
            if (edge == null)
            {
                return;
            }

            edge.input?.Disconnect(edge);
            edge.output?.Disconnect(edge);
            RemoveElement(edge);
        }

        private void ConnectPorts(Port outputPort, Port inputPort)
        {
            if (outputPort == null || inputPort == null)
            {
                return;
            }

            AddElement(outputPort.ConnectTo(inputPort));
        }

        private static Rect Union(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
