using System;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一次「说一句」的请求。
    /// ★ 气泡层只认这里面的东西，不认识 DialogueShowTextItem / DialogueSpeaker 之类的对话数据，
    ///   所以这套气泡系统可以脱离对话单独用（漫画过场、教学提示、拟声词都行）。
    /// </summary>
    public struct BubbleRequest
    {
        /// <summary>谁在说。气泡按 key 认领；同一个 key 再 Show = 顶掉（复用同一个气泡换文本）</summary>
        public string key;

        /// <summary>正文</summary>
        public string text;

        /// <summary>气泡上显示的名字（可空 = 不显示）</summary>
        public string displayName;

        /// <summary>这一句用哪套样式（可空 = 用气泡实例上的默认样式）</summary>
        public BubbleStyleSO style;

        /// <summary>打字速度覆盖，秒/字（&lt;= 0 = 用样式里的）</summary>
        public float speedOverride;

        /// <summary>这一句打完之后回调（可空）</summary>
        public Action onTyped;
    }
}
