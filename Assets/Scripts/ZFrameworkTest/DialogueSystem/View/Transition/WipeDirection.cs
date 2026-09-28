using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 擦除方向 —— 所有擦除实现共用同一个枚举，策划/美术换方向不用重新学。
    /// 名字里的"从…到…"就是纸扫过去的方向（观众看到纸从哪边进、往哪边出）。
    ///
    /// ★ <see cref="ComponentDefault"/> 是给**组数据**用的哨兵值：
    ///   组头不填方向时用它，含义是"不覆盖，用擦除组件上配的那个方向"。
    ///   它排在最后（= 100），不影响已有数据里存着的 0~5。
    /// </summary>
    public enum WipeDirection
    {
        [InspectorName("从左往右")]
        LeftToRight = 0,

        [InspectorName("从右往左")]
        RightToLeft = 1,

        [InspectorName("从下往上")]
        BottomToTop = 2,

        [InspectorName("从上往下")]
        TopToBottom = 3,

        [InspectorName("斜着：左下 → 右上")]
        DiagonalUp = 4,

        [InspectorName("斜着：左上 → 右下")]
        DiagonalDown = 5,

        /// <summary>只在组数据里用：不覆盖，用擦除组件上配的方向</summary>
        [InspectorName("（用组件上的方向）")]
        ComponentDefault = 100,
    }

    /// <summary>方向 → 角度（纸在本地空间里朝哪边推，单位：度，绕 Z 轴）</summary>
    public static class WipeDirectionUtil
    {
        /// <summary>组数据里的值是不是"要覆盖"（ComponentDefault = 不覆盖）</summary>
        public static bool IsOverride(WipeDirection direction)
        {
            return direction != WipeDirection.ComponentDefault;
        }

        public static float ToAngle(WipeDirection direction)
        {
            switch (direction)
            {
                case WipeDirection.RightToLeft: return 180f;
                case WipeDirection.BottomToTop: return 90f;
                case WipeDirection.TopToBottom: return 270f;
                case WipeDirection.DiagonalUp: return 45f;
                case WipeDirection.DiagonalDown: return -45f;
                default: return 0f;             // LeftToRight
            }
        }

        /// <summary>推进方向（单位向量）——本地 +X 转到父空间里的朝向</summary>
        public static Vector2 ToAxis(WipeDirection direction)
        {
            float rad = ToAngle(direction) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
