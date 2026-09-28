using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 气泡样式：决定"这一句长什么样"。
    /// 位置不在这里（位置在模板 Prefab 里摆），这里只管外观 + 该用哪套动画。
    /// 一个样式资产可以给很多句台词共用；喊叫 / 心声 / 旁白 / 拟声词就是各自一个样式资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Presentation/Bubble Style", fileName = "BubbleStyle_")]
    public class BubbleStyleSO : ScriptableObject
    {
        [Header("外观")]
        [Label("底图（建议 9-slice）")]
        public Sprite frameSprite;

        [Label("正文颜色")]
        public Color textColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Label("正文字号")]
        public float fontSize = 30f;

        [Label("文字最大宽度（超过就换行）")]
        public float maxTextWidth = 420f;

        [Header("内边距（气泡框比文字大多少）")]
        [Label("左右")]
        public float padX = 30f;

        [Label("上下")]
        public float padY = 20f;

        [Header("名字（可关）")]
        [Label("显示名字")]
        public bool showName = true;

        [Label("名字字号")]
        public float nameFontSize = 24f;

        [Label("名字颜色")]
        public Color nameColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Label("名字和正文的间距")]
        public float nameSpacing = 6f;

        [Header("其它")]
        [Label("打完字显示继续箭头 ▼")]
        public bool showArrow = true;

        [Label("动画 / 音效 / 打字速度")]
        public BubbleAnimSet animSet;
    }
}
