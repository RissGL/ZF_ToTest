using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 组头节点：放在分组框里的第一条，承载该组的分镜模板、组音效、以及分叉选项。
    /// 四个出口/入口：
    ///   in          ← 上一组 / 别人跳进来
    ///   out         → 组内第一句对话
    ///   nextGroup   → 线性下一组（不分叉时走这条）
    ///   choice_0..N → 分叉：每个选项一个端口，连到目标组的 in（或目标组里的任意节点）
    ///
    /// ★ 选项行（文本框）和选项端口（choice_下标）是按顺序一一对应的：
    ///   端口名里的数字就是文本框的下标，删掉中间一个会把后面的端口名重排。
    /// </summary>
    public class GroupNodeView : Node
    {
        /// <summary>选项端口名前缀：端口名 = choice_下标</summary>
        public const string ChoicePortPrefix = "choice_";

        public string GUID;

        /// <summary>组间入口（接收上一组）</summary>
        public Port InputPort;
        /// <summary>组内出口（连组内第一句对话）</summary>
        public Port OutputPort;
        /// <summary>组间出口（线性下一组；要分叉请用选项端口）</summary>
        public Port NextPort;

        /// <summary>内容变了（图编辑器用它记脏 + 压撤销栈），由 DialogueGraphView 挂上</summary>
        public Action OnChanged;

        /// <summary>标题里显示的组 id，由 DialogueGraphView 提供（= 本组头所在分组框的标题）</summary>
        public Func<string> GroupTitleProvider;

        /// <summary>是不是本章的起始组（检查 / 导出后由窗口标记，只影响显示）</summary>
        public bool IsStartGroup { get; private set; }

        private VisualElement m_ChoiceContainer;
        private readonly List<TextField> m_ChoiceFields = new List<TextField>();
        private readonly List<Port> m_ChoicePorts = new List<Port>();

        /// <summary>固定字段（分镜模板 / 组音效）的控件，按 <see cref="DialogueNodeFields.Group"/> 建</summary>
        private readonly Dictionary<string, VisualElement> m_Controls = new Dictionary<string, VisualElement>();

        public int ChoiceCount => m_ChoicePorts.Count;

        public Port GetChoicePort(int index)
            => index >= 0 && index < m_ChoicePorts.Count ? m_ChoicePorts[index] : null;

        public string GetChoiceText(int index)
            => index >= 0 && index < m_ChoiceFields.Count ? m_ChoiceFields[index].value : null;

        public GroupNodeView()
        {
            GUID = Guid.NewGuid().ToString();
            title = "组头";

            // ---- 入口：接收上一组 ----
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);
            InputPort.portColor = new Color(0.55f, 1f, 0.55f);

            // ---- 出口 1：组内第一句 ----
            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            OutputPort.portName = "out";
            outputContainer.Add(OutputPort);
            OutputPort.portColor = new Color(1f, 0.8f, 0.3f);

            // ---- 出口 2：线性下一组 ----
            NextPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            NextPort.portName = "nextGroup";
            NextPort.tooltip = "线性下一组：不分叉时走这条（只应该连一条；要分叉请用下面的选项端口）";
            outputContainer.Add(NextPort);
            NextPort.portColor = new Color(0.5f, 0.8f, 1f);

            // ---- 固定字段：按 DialogueNodeFields.Group 这张表建（加一个组级字段只改表）----
            foreach (var def in DialogueNodeFields.Group)
            {
                var control = def.CreateControl();
                m_Controls[def.Name] = control;
                mainContainer.Add(control);

                control.RegisterCallback<ChangeEvent<string>>(_ => OnChanged?.Invoke());
                control.RegisterCallback<ChangeEvent<UnityEngine.Object>>(_ => OnChanged?.Invoke());
                control.RegisterCallback<ChangeEvent<Enum>>(_ => OnChanged?.Invoke());
            }

            // ---- 选项（分叉）区 ----
            var choiceLabel = new Label("选项（每个选项一个端口 → 目标组的 in）");
            choiceLabel.style.marginTop = 6f;
            choiceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainContainer.Add(choiceLabel);

            m_ChoiceContainer = new VisualElement();
            mainContainer.Add(m_ChoiceContainer);

            var addButton = new Button(() => AddChoiceRow($"选项{m_ChoiceFields.Count + 1}")) { text = "+ 选项" };
            addButton.style.height = 18f;
            addButton.style.marginTop = 2f;
            mainContainer.Add(addButton);

            // 标题上色，区分组节点
            var titleLabel = titleContainer.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.style.color = new Color(1f, 0.85f, 0.4f);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        // ===================== 字段读写（都按名字走字段表） =====================

        public VisualElement Control(string fieldName)
        {
            return m_Controls.TryGetValue(fieldName, out var control) ? control : null;
        }

        public object ReadField(string fieldName)
        {
            return DialogueNodeFields.FindGroup(fieldName)?.Read(Control(fieldName));
        }

        public void WriteField(string fieldName, object value)
        {
            DialogueNodeFields.FindGroup(fieldName)?.Write(Control(fieldName), value);
        }

        public string GetText(string fieldName)
        {
            return ReadField(fieldName) as string ?? string.Empty;
        }

        // ===================== 选项行 =====================

        /// <summary>加一行选项：左边填选项文本，右边对应一个输出端口</summary>
        private void AddChoiceRow(string text)
        {
            int index = m_ChoicePorts.Count;

            var field = new TextField();
            field.value = text;
            field.style.flexGrow = 1f;
            field.style.marginLeft = 0f;

            var removeButton = new Button(() => RemoveChoiceRow(m_ChoiceFields.IndexOf(field))) { text = "×" };
            removeButton.style.width = 18f;
            removeButton.style.height = 16f;
            removeButton.tooltip = "删掉这个选项";

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.Add(field);
            row.Add(removeButton);

            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = $"{ChoicePortPrefix}{index}";
            port.portColor = new Color(1f, 0.55f, 0.9f);
            port.tooltip = text;

            // 改文本时同步 tooltip（线多的时候靠它辨认），并通知编辑器记脏
            field.RegisterValueChangedCallback(evt =>
            {
                port.tooltip = evt.newValue;
                OnChanged?.Invoke();
            });

            m_ChoiceFields.Add(field);
            m_ChoicePorts.Add(port);
            m_ChoiceContainer.Add(row);
            outputContainer.Add(port);

            RefreshExpandedState();
            RefreshPorts();

            OnChanged?.Invoke();
        }

        /// <summary>删掉第 index 行（连出去的线一起删，后面的端口名重排）</summary>
        private void RemoveChoiceRow(int index)
        {
            if (index < 0 || index >= m_ChoicePorts.Count)
            {
                return;
            }

            // 1) 先把这个端口上的线从图上摘掉（只摘元素不移除会留下悬空的线）
            var graph = GetFirstAncestorOfType<GraphView>();
            var port = m_ChoicePorts[index];
            foreach (var edge in port.connections.ToList())
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                graph?.RemoveElement(edge);
            }

            // 2) 移除端口和整行 UI
            outputContainer.Remove(port);
            m_ChoicePorts.RemoveAt(index);

            var field = m_ChoiceFields[index];
            var row = field.parent;
            m_ChoiceFields.RemoveAt(index);
            row?.RemoveFromHierarchy();

            // 3) 端口名 = 下标，删了中间一个要把后面的重排，否则导出会对错选项
            for (int i = index; i < m_ChoicePorts.Count; i++)
            {
                m_ChoicePorts[i].portName = $"{ChoicePortPrefix}{i}";
            }

            RefreshExpandedState();
            RefreshPorts();

            OnChanged?.Invoke();
        }

        private void ClearChoiceRows()
        {
            for (int i = m_ChoicePorts.Count - 1; i >= 0; i--)
            {
                RemoveChoiceRow(i);
            }
        }

        // ===================== 标题 =====================

        /// <summary>标题 =「组 id 组头」（起始组加 ★），组 id 就是分组框的标题 = 导出后的 groupId</summary>
        public void RefreshTitle()
        {
            string group = GroupTitleProvider?.Invoke();
            string groupLabel = string.IsNullOrEmpty(group) ? "（未归组）" : group;

            title = IsStartGroup ? $"★ {groupLabel} 组头" : $"{groupLabel} 组头";
        }

        /// <summary>标记起始组（只影响显示）</summary>
        public void SetStartGroup(bool isStart)
        {
            IsStartGroup = isStart;
            RefreshTitle();
        }

        // ===================== 数据读写 =====================

        public DialogueGraphData.NodeData ToData()
        {
            RefreshTitle();      // 先把标题算到最新：节点名就取它（SO 里靠它认人，不是靠 guid）

            var data = new DialogueGraphData.NodeData
            {
                guid = GUID,
                displayName = title,
                kind = DialogueGraphData.NodeKind.Group,
                position = GetPosition().position,
            };

            DialogueNodeFields.ApplyToNodeData(this, data);

            data.choiceTexts = new List<string>();
            foreach (var field in m_ChoiceFields)
            {
                data.choiceTexts.Add(field.value);
            }

            return data;
        }

        public void SetData(DialogueGraphData.NodeData data)
        {
            // ★ 不要继承空 guid：旧数据里出现过组头 guid 为空，一旦继承下去，
            //   从组头连出去的线（out / nextGroup / 选项口）永远存不下来
            GUID = string.IsNullOrEmpty(data.guid) ? Guid.NewGuid().ToString() : data.guid;

            DialogueNodeFields.ApplyFromNodeData(this, data);

            // 选项行按数据重建（端口也跟着重建，端口名重新从 choice_0 开始）
            ClearChoiceRows();
            if (data.choiceTexts != null)
            {
                foreach (var text in data.choiceTexts)
                {
                    AddChoiceRow(text);
                }
            }
        }
    }
}
