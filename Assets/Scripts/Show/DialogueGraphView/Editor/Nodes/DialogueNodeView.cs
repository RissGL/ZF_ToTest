using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话 item 节点：角色 / 台词 / 表情 / 音效 / 气泡样式 / 槽 id。
    /// 输入：接上一个对话或组头；输出：接下一个对话（组内顺序靠它串）。
    ///
    /// ★ 字段**不是写死在这里**的：控件按 <see cref="DialogueNodeFields.Line"/> 这张表建出来，
    ///   存取数据也走那张表 —— 加一个台词字段只改表 + 运行时数据类，
    ///   不用再回来改"控件声明 / 创建 / 事件注册 / 存 / 读"这五处。
    /// </summary>
    public class DialogueNodeView : Node
    {
        public string GUID;

        public Port InputPort;
        public Port OutputPort;

        /// <summary>内容变了（图编辑器用它记脏 + 压撤销栈），由 DialogueGraphView 挂上</summary>
        public Action OnChanged;

        /// <summary>
        /// 标题里显示的组 id，由 DialogueGraphView 提供（= 节点所在分组框的标题）。
        /// 节点自己不知道属于哪一组 —— 组是"框"表达出来的，只有图编辑器能算。
        /// </summary>
        public Func<string> GroupTitleProvider;

        private readonly Dictionary<string, VisualElement> m_Controls = new Dictionary<string, VisualElement>();

        public DialogueNodeView()
        {
            GUID = Guid.NewGuid().ToString();
            title = "对话 item";

            // ---- 端口 ----
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);

            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            OutputPort.portName = "out";
            outputContainer.Add(OutputPort);

            InputPort.portColor = new Color(0.35f, 0.85f, 1f);
            OutputPort.portColor = new Color(0.35f, 0.85f, 1f);

            BuildFields();

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>按字段表建控件；任意一个变了就刷新标题 + 通知编辑器（记脏/撤销栈）</summary>
        private void BuildFields()
        {
            foreach (var def in DialogueNodeFields.Line)
            {
                var control = def.CreateControl();
                m_Controls[def.Name] = control;
                mainContainer.Add(control);

                // 各种控件的 ChangeEvent 泛型参数各不相同，全注册一遍：
                // 只有自己那种会真的触发，也就不会漏掉任何一种
                control.RegisterCallback<ChangeEvent<string>>(_ => NotifyChanged());
                control.RegisterCallback<ChangeEvent<UnityEngine.Object>>(_ => NotifyChanged());
                control.RegisterCallback<ChangeEvent<Enum>>(_ => NotifyChanged());
                control.RegisterCallback<ChangeEvent<Color>>(_ => NotifyChanged());
                control.RegisterCallback<ChangeEvent<float>>(_ => NotifyChanged());
            }
        }

        // ===================== 字段读写（都按名字走字段表） =====================

        public VisualElement Control(string fieldName)
        {
            return m_Controls.TryGetValue(fieldName, out var control) ? control : null;
        }

        public object ReadField(string fieldName)
        {
            return DialogueNodeFields.Find(fieldName)?.Read(Control(fieldName));
        }

        public void WriteField(string fieldName, object value)
        {
            DialogueNodeFields.Find(fieldName)?.Write(Control(fieldName), value);
        }

        public string GetText(string fieldName)
        {
            return ReadField(fieldName) as string ?? string.Empty;
        }

        public T GetObject<T>(string fieldName) where T : UnityEngine.Object
        {
            return ReadField(fieldName) as T;
        }

        private void NotifyChanged()
        {
            RefreshTitle();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 标题 =「组 id / 角色名：台词摘要」。
        /// ★ 组 id 放最前面：节点一多，光看角色和台词分不清谁在哪一组，
        ///   也没法跟图数据里的 groupTitle 对上。不在任何框里就显式写「（未归组）」——
        ///   这种节点导出时会被跳过，摆在面板上就是要让人一眼看见。
        /// </summary>
        public void RefreshTitle()
        {
            string group = GroupTitleProvider?.Invoke();
            string groupLabel = string.IsNullOrEmpty(group) ? "（未归组）" : group;

            var speaker = GetObject<DialogueSpeaker>("speaker");
            string speakerName = speaker != null && !string.IsNullOrEmpty(speaker.name) ? speaker.name : "（无角色）";

            string text = GetText("text");
            if (string.IsNullOrEmpty(text))
            {
                title = $"{groupLabel} / {speakerName}：（空台词）";
                return;
            }

            string oneLine = text.Replace("\r", " ").Replace("\n", " ");
            title = $"{groupLabel} / {speakerName}：" +
                    (oneLine.Length <= 14 ? oneLine : oneLine.Substring(0, 14) + "…");
        }

        // ===================== 数据读写 =====================

        public DialogueGraphData.NodeData ToData()
        {
            RefreshTitle();      // 先把标题算到最新：节点名就取它（SO 里靠它认人，不是靠 guid）

            var data = new DialogueGraphData.NodeData
            {
                guid = GUID,
                displayName = title,
                kind = DialogueGraphData.NodeKind.Dialogue,
                position = GetPosition().position,
            };

            DialogueNodeFields.ApplyToNodeData(this, data);
            return data;
        }

        public void SetData(DialogueGraphData.NodeData data)
        {
            // ★ 不要继承空 guid：旧数据里出现过节点 guid 为空，一旦继承下去，
            //   从它连出去的线永远存不下来（保存时端点 guid 是空的，读回来时找不到节点）
            GUID = string.IsNullOrEmpty(data.guid) ? Guid.NewGuid().ToString() : data.guid;

            DialogueNodeFields.ApplyFromNodeData(this, data);
            RefreshTitle();
        }
    }
}
