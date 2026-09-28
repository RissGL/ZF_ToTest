using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 分镜背景：这一格的底色/底板。和立绘框、立绘一样是槽里的一个独立部件（SlotVisual 子类）。
    ///
    /// ★ 它为什么独立成脚本：背景挂在槽里、和框是**兄弟节点**（放进框里面会有层级问题），
    ///   所以框的 CanvasGroup 管不到它 —— 与其让框去驱动它，不如让它自己也是一个"会演出的部件"：
    ///   收起 / 入场 / 退场 / 说话人高亮，全部由 SlotVisual 这套统一处理（BubbleStage 换格时会一起驱动）。
    ///
    /// ★ 颜色：**代码永远不碰 Image 的颜色**（RGB 和 alpha 都是你在编辑器里配的值）。
    ///   要"显示时半透明底色、隐藏时没有底色"，靠的是本物体上的 **CanvasGroup**：
    ///     收起 → group.alpha = hiddenAlpha（默认 0）
    ///     显示 → group.alpha = 1（Image 自己的 alpha 原样生效，比如 0.396 的半透明底）
    ///     非说话人 → group.alpha = dimAlpha（变暗，颜色仍然不变）
    ///   所以亮/暗完全由 CanvasGroup 表达，配色不会被改。
    ///
    /// 想跟框一起缩放、或做自己的入场动画：给它自己的 animSet（作用于本物体）或在
    /// enterGroups 里配（目标可指向它下面的子物体）。
    /// </summary>
    public class PortraitBackground : SlotVisual
    {
        [Label("背景图（留空 = 取本物体上的 Image）")]
        public Image image;

        protected override void Awake()
        {
            base.Awake();

            if (image == null)
            {
                image = GetComponent<Image>();
            }
        }

        // 不重写 OnHighlightColor：背景的颜色是配好的，亮/暗只走 CanvasGroup 的 alpha
    }
}
