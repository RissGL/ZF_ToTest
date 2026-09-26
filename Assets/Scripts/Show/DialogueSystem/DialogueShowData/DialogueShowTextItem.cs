using System;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一句台词：角色 + 表情 + 文本 + 音效 + 气泡样式 + 槽 id。
    /// [Serializable] 必须有，否则它在 List 里 Unity 不序列化（和 MangaAnimEffect 同一个坑）。
    /// </summary>
    [Serializable]
    public class DialogueShowTextItem
    {
        [Label("角色")]
        public DialogueSpeaker  speaker;
        
        [Label("文本")]
        public string text;
        
        [Label("表情")]
        public DialogueCharExpressionEnum expression;

        [Label("音效")]
        public ShowAudioEventSO audioEff;

        [Label("气泡样式（空 = 用槽里气泡的默认样式）")]
        public BubbleStyleSO bubbleStyle;

        /// <summary>
        /// 槽 id（空 = 自动分配）。
        ///
        /// 模板里的位置是「槽」，不是「角色」—— 所以同一句话想固定出现在某个位置时填槽 id 就行，
        /// 比如「这句一定要在右边的槽说」填 slotId = "右"。
        /// 不填的话：本格内这个角色第一次说话会领一个空槽，之后他在本格里固定用那个槽。
        /// 旁白（speaker 为空）会优先领「没有立绘框」的槽。
        /// </summary>
        [Label("槽 id（空 = 自动分配）")]
        public string slotId;
    }
}
