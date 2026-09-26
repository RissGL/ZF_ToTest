using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话图数据（测试用）：把图上的节点与连线序列化成一个 SO。
    /// 创建：右键 Create → DialogueGraph → Graph Data
    /// </summary>
    [CreateAssetMenu(menuName = "DialogueGraph/Graph Data", fileName = "DialogueGraphData")]
    public class DialogueGraphData : ScriptableObject
    {
        public enum NodeKind
        {
            Group,      // 分组节点（一组对话的分镜/转场）
            Dialogue,   // 对话 item 节点（一句话）
        }

        [Serializable]
        public class NodeData
        {
            [Label("节点 guid（内部用，别手改）")]
            public string guid;
            [Label("节点类型")]
            public NodeKind kind;
            [Label("位置")]
            public Vector2 position;

            /// <summary>
            /// 给人看的节点名（保存时由编辑器写入，形如「Group_1 / 阿岚：喂，你看见…」）。
            /// 只读用途：SO 的 Inspector 和 .asset 文本里光看 guid 是一串乱码，
            /// 有它才能一眼看出这个节点是哪一组的哪一句。
            /// ★ 它不是权威数据，导出/运行时都不用；改标题的规则在节点视图的 RefreshTitle 里。
            /// </summary>
            [Label("节点名（保存时自动写，只读用）")]
            public string displayName;

            /// <summary>所属分组框的标题（空 = 不属于任何组）</summary>
            [Label("所属分组框（= 组 id；空 = 未归组，导出会跳过）")]
            public string groupTitle;

            // === Group 节点字段 ===
            [Label("分镜模板 id（组头用，空 = 0）")]
            public string templateId;

            [Label("离场动画 id（空 = 本格离场时直接切）")]
            public string transitionId;

            [Label("离场方向（用组件上的 = 不覆盖）")]
            public WipeDirection transitionDirection = WipeDirection.ComponentDefault;

            [Label("离场颜色（Alpha = 0 = 不覆盖）")]
            public Color transitionColor = new Color(0f, 0f, 0f, 0f);

            [Label("离场动画时长（秒；0 = 用组件上的）")]
            public float transitionDuration;

            // ★ 不能叫 bubbleStyle：台词节点上已经有一个同名的（那是"这一句的气泡样式"）。
            //   组级这个用字段表的 RuntimeName 映射到运行时的 bubbleStyle。
            [Label("本格默认气泡样式（空 = 用气泡自己的默认）")]
            public BubbleStyleSO groupBubbleStyle;

            [Label("本格出场角色（不说话的也会站在场上）")]
            public List<DialogueSpeaker> cast = new List<DialogueSpeaker>();

            /// <summary>分叉选项文本。下标 i ↔ 输出端口 choice_i（顺序不能乱，导出时就是这个顺序）</summary>
            [Label("选项文本（下标 0 对应 choice_0 端口）")]
            public List<string> choiceTexts = new List<string>();

            // === 共用：音效（Group 和 Dialogue 都能挂）===
            [Label("音效（组头 = 进组音效；台词 = 这句的音效）")]
            public ShowAudioEventSO audio;

            // === Dialogue 节点字段 ===
            [Label("角色（空 = 旁白）")]
            public DialogueSpeaker speaker;
            [Label("台词")]
            public string text;
            [Label("表情")]
            public DialogueCharExpressionEnum expression;

            /// <summary>这句台词用哪套气泡样式（空 = 用气泡实例上的默认样式）</summary>
            [Label("气泡样式（空 = 用模板里气泡的默认样式）")]
            public BubbleStyleSO bubbleStyle;

            /// <summary>槽 id（空 = 自动分配）：把这句钉在模板里的某个位置时说</summary>
            [Label("槽 id（空 = 自动分配位置）")]
            public string slotId;
        }

        /// <summary>分组框（视觉容器）：一组对话的框，节点拖进去即归组</summary>
        [Serializable]
        public class GroupBoxData
        {
            [Label("框标题（= 组 id，全局唯一）")]
            public string title;
            [Label("位置")]
            public Vector2 position;
            [Label("尺寸")]
            public Vector2 size;
        }

        [Serializable]
        public class LinkData
        {
            [Label("起点节点 guid")]
            public string fromGuid;      // 输出端节点 guid
            [Label("终点节点 guid")]
            public string toGuid;        // 输入端节点 guid

            /// <summary>输出端口名：out（组内第一句）/ next（下一组）。组头有两个出口，必须记录连的是哪个</summary>
            [Label("起点端口名（out / nextGroup / choice_N）")]
            public string fromPortName;
        }

        /// <summary>手填的起始组 id（空 = 导出时自动推断：没有任何外来线连进来的那个组）</summary>
        [Label("起始组 id（空 = 自动推断）")]
        public string startGroupId;

        [Label("节点列表")]
        public List<NodeData> nodes = new List<NodeData>();
        [Label("连线列表")]
        public List<LinkData> links = new List<LinkData>();
        [Label("分组框列表（框标题 = 组 id）")]
        public List<GroupBoxData> groupBoxes = new List<GroupBoxData>();
    }
}
