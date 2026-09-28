using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 立绘框：分镜里的「画格」。
    ///
    /// ★ 和立绘（Portrait）、背景（PortraitBackground）都是**兄弟节点**，不是父子：
    ///   框怎么缩放、怎么滑进来，都不会带着它们一起变。
    ///
    /// ★ 代码不知道框里有什么部件：几条边、有没有花纹，全是这个 Prefab 自己的事。
    ///   动画通过两个口驱动：
    ///     ① animSet（共享资产）：效果作用于本物体（整个框）
    ///     ② enterGroups / exitGroups：每组 = 目标 + 效果，目标可以是框内任意子物体
    ///
    /// 层级（模板 Prefab 里）：
    ///   Slot_左
    ///     ├─ Frame_左               ← 本组件（框）
    ///     ├─ Portrait_左            ← 立绘
    ///     ├─ Background_左          ← 背景（挂 PortraitBackground）
    ///     └─ Bubble_左              ← 气泡
    /// </summary>
    public class PortraitFrame : SlotVisual
    {
        [Label("框的视觉（留空 = 取本物体上的 Image；没有就是纯位置框）")]
        public Image frame;

        protected override void Awake()
        {
            base.Awake();

            if (frame == null)
            {
                frame = GetComponent<Image>();
            }
        }

        protected override void OnHighlightColor(bool isSpeaker)
        {
            if (frame != null)
            {
                frame.color = isSpeaker ? normalColor : dimColor;
            }
        }
    }
}
