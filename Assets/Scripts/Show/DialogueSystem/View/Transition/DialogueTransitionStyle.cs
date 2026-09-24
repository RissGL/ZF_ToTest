using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一组"由数据传进来的擦除样式"：方向 / 颜色 / 时长。
    /// 组头可以只填这几样，让**同一套擦除组件**服务所有方向、所有颜色、所有时长 ——
    /// 不用再为"从左往右"和"从右往左"各配一套组件。
    ///
    /// 没填的项（HasXxx = false）实现会退回组件上配的默认值。
    /// </summary>
    public struct DialogueTransitionStyle
    {
        /// <summary>组数据里指定了方向</summary>
        public bool HasDirection;

        public WipeDirection Direction;

        /// <summary>组数据里指定了颜色（颜色 Alpha = 0 视为没填）</summary>
        public bool HasColor;

        public Color Color;

        /// <summary>组数据里指定了时长（&lt;= 0 视为没填）</summary>
        public bool HasDuration;

        /// <summary>时长（秒）</summary>
        public float Duration;

        /// <summary>从组数据拼一份（全部为空 = 组件上的默认值）</summary>
        public static DialogueTransitionStyle From(DialogueShowGroupData group)
        {
            var style = new DialogueTransitionStyle();

            if (group == null)
            {
                return style;
            }

            if (WipeDirectionUtil.IsOverride(group.transitionDirection))
            {
                style.HasDirection = true;
                style.Direction = group.transitionDirection;
            }

            // 颜色：Alpha = 0 当作"没填"（Unity 彩色选择器里 alpha 归零很自然）
            if (group.transitionColor.a > 0f)
            {
                style.HasColor = true;
                style.Color = group.transitionColor;
            }

            // 时长：0 或负数当作"没填"
            if (group.transitionDuration > 0f)
            {
                style.HasDuration = true;
                style.Duration = group.transitionDuration;
            }

            return style;
        }

        /// <summary>有没有任何要覆盖的</summary>
        public bool IsEmpty => !HasDirection && !HasColor && !HasDuration;
    }

    /// <summary>
    /// 「我的方向 / 颜色 / 时长可以由组数据覆盖」——擦除实现想要这个能力就实现它。
    ///
    /// 时序器在解析出擦除、开播之前会调一次 <see cref="ApplyStyle"/>；
    /// 没实现这个接口的擦除就只用组件上配的参数（行为不变）。
    ///
    /// ★ 实现注意：**没指定的项要退回组件上的原值**，不能沿用上一次被覆盖的值 ——
    ///   否则"上一格用从右往左、这一格没填"就会错误地继续从右往左。
    /// </summary>
    public interface IDialogueTransitionStyle
    {
        void ApplyStyle(DialogueTransitionStyle style);
    }
}
