using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    using System;
    using UnityEngine;
    using DG.Tweening;
    using UnityEngine.UI;

    /// <summary>
    /// 缩放 / 位移作用在什么地方。
    /// ★ UI 上这两者效果不一样：
    ///   - Transform：改 localScale。连九宫格边框厚度、字号一起缩（漫画气泡一直这么用）
    ///   - Rect：改 RectTransform 的 sizeDelta。只有矩形变大变小，边框和字不受影响（界面推荐）
    /// </summary>
    public enum VisualScaleMode
    {
        /// <summary>改 transform（localScale / localPosition）。默认值，老的资源不填就是这个</summary>
        Transform,

        /// <summary>改 RectTransform（sizeDelta / anchoredPosition）</summary>
        Rect,
    }

    /// <summary>
    /// 动画效果基类，子类实现造什么动画
    /// </summary>
    [Serializable]
    public abstract class MangaAnimEffect
    {
        [Label("本效果延迟") ]
        [SerializeField] protected float delay;
        
        
        /// <summary>
        /// 造动画不播放：由组件的 seq 统一开播
        /// </summary>
        public abstract Tween CreateTween(Transform target);

        /// <summary>
        /// 子类便捷方法：造完 tween 统一加 delay + 暂停（等 seq 指挥）
        /// </summary>
        protected Tween WithDelayAndPause(Tween tween)
            => tween.SetDelay(delay).Pause();
        
        /// <summary>
        /// 用于重置动画状态看上一张的时候可以重新播放
        /// </summary>
        public abstract  void ResetAni(Transform target);
        
    }


    [Serializable]
    public class MoveEffect : MangaAnimEffect
    {
        [Label("作用对象（Rect = 改 anchoredPosition，界面推荐）")]
        [SerializeField] private VisualScaleMode targetMode;

        [Label("位移量（相对当前位置）")]
        [SerializeField] private Vector3 delta;
        [Label("时长（秒）")]
        [SerializeField] private float duration = 0.5f;
        [Label("缓动曲线")]
        [SerializeField] private Ease ease = Ease.OutQuad;

        public override Tween CreateTween(Transform target)
        {
            // ---- 界面：挪 RectTransform 的位置 ----
            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                return WithDelayAndPause(
                    rect.DOAnchorPos(rect.anchoredPosition + new Vector2(delta.x, delta.y), duration).SetEase(ease));
            }

            return WithDelayAndPause(
                target.DOLocalMove(target.localPosition + delta, duration).SetEase(ease));
        }

        public override void ResetAni(Transform  target)
        {
            // 反着挪回去：这样即使没播过动画也能安全重置
            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                rect.anchoredPosition -= new Vector2(delta.x, delta.y);
                return;
            }

            target.localPosition-=delta;
        }
    }

    [Serializable]
    public class ScaleEffect : MangaAnimEffect
    {
        [Label("作用对象（Rect = 改尺寸，界面推荐；Transform = 缩 localScale）")]
        [SerializeField] private VisualScaleMode targetMode;

        private Vector3 beforeScale;
        private Vector2 beforeSize;
        private bool captured;

        [Label("用起始缩放（不勾 = 从当前缩放开始）")]
        [SerializeField] private bool useFromScale;

        [Label("起始缩放（勾上「用起始缩放」才生效，比如 0.94 = 从小弹出来）")]
        [SerializeField] private Vector3 fromScale = new Vector3(0.94f, 0.94f, 1f);

        [Label("目标缩放（Transform 模式 = 绝对缩放；Rect 模式 = 尺寸倍数，三个 1 = 不变）")]
        [SerializeField] private Vector3 targetScale = Vector3.one;
        [Label("时长（秒）")]
        [SerializeField] private float duration = 0.5f;
        [Label("缓动曲线")]
        [SerializeField] private Ease ease = Ease.OutBack;

        public override Tween CreateTween(Transform target)
        {
            // ---- 界面：改 RectTransform 的尺寸（不缩边框、不缩字）----
            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                beforeSize = rect.sizeDelta;
                captured = true;

                if (useFromScale)
                {
                    // "从指定尺寸弹出来"由这条动画自己负责 ——
                    // 这样静息态（hiddenScale）就可以保持 1，运行时和编辑器完全一致
                    rect.sizeDelta = new Vector2(beforeSize.x * fromScale.x, beforeSize.y * fromScale.y);
                }

                // 终点 = 美术摆的尺寸 × 目标倍数（和 Transform 模式的"绝对缩放"一一对应）
                return WithDelayAndPause(
                    rect.DOSizeDelta(new Vector2(beforeSize.x * targetScale.x, beforeSize.y * targetScale.y), duration).SetEase(ease));
            }

            // ---- 常规：缩 transform ----
            beforeScale = target.localScale;
            captured = true;

            if (useFromScale)
            {
                // "从指定缩放弹出来"由这条动画自己负责 ——
                // 这样静息态（hiddenScale）就可以保持 1，运行时和编辑器完全一致
                target.localScale = fromScale;
            }

            return WithDelayAndPause(
                target.DOScale(targetScale, duration).SetEase(ease));
        }

        public override void ResetAni(Transform target)
        {
            // 没播过就没状态要重置（否则会把缩放/尺寸重置成 0）
            if (!captured)
            {
                return;
            }

            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                rect.sizeDelta = beforeSize;
                return;
            }

            target.localScale=beforeScale;
        }
    }

    [Serializable]
    public class FadeEffect : MangaAnimEffect
    {
         private float beforeAlpha=0f;
        [Label("目标透明度（1 = 不透明，0 = 全透明）")]
        [SerializeField] private float alpha = 1f;
        [Label("时长（秒）")]
        [SerializeField] private float duration = 0.5f;

        public override Tween CreateTween(Transform target)
        {
            var graphic=target.GetComponent<Graphic>();
            beforeAlpha=graphic.color.a;
           return WithDelayAndPause(
                graphic.DOFade(alpha, duration));
        }

        public override void ResetAni(Transform target)
        {
            var graphic=target.GetComponent<Graphic>();
            var  color=graphic.color;
            color.a = beforeAlpha;
            graphic.color=color;
        }
    }

    [Serializable]
    public class PunchScaleEffect : MangaAnimEffect
    {
        [Label("作用对象（Rect = 弹尺寸，界面推荐）")]
        [SerializeField] private VisualScaleMode targetMode;

        private Vector3 beforeScale;
        private Vector2 beforeSize;
        private bool captured;

        [Label("弹一下的幅度")]
        [SerializeField] private Vector3 punch = new Vector3(0.4f, 0.4f, 0f);
        [Label("时长（秒）")]
        [SerializeField] private float duration = 0.4f;

        public override Tween CreateTween(Transform target)
        {
            // ---- 界面：尺寸弹一下（大一点再回来）----
            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                beforeSize = rect.sizeDelta;
                captured = true;

                Vector2 peak = new Vector2(beforeSize.x * (1f + punch.x), beforeSize.y * (1f + punch.y));

                return WithDelayAndPause(
                    DOTween.Sequence()
                        .Append(rect.DOSizeDelta(peak, duration * 0.5f).SetEase(Ease.OutQuad))
                        .Append(rect.DOSizeDelta(beforeSize, duration * 0.5f).SetEase(Ease.OutQuad)));
            }

            beforeScale=target.localScale;
            captured = true;
            return WithDelayAndPause(
                target.DOPunchScale(punch, duration));
        }

        public override void ResetAni(Transform target)
        {
            if (!captured)
            {
                return;
            }

            if (targetMode == VisualScaleMode.Rect && target is RectTransform rect)
            {
                rect.sizeDelta = beforeSize;
                return;
            }

            target.localScale=beforeScale;
        }
    }

    /// <summary>
    /// 整个 CanvasGroup 淡入淡出（连子物体一起淡）。
    /// ★ 和 FadeEffect 的区别：FadeEffect 只淡"同一个物体上的那一个 Graphic"，
    ///   不会带着子物体（比如气泡里的 TMP 文字）一起淡。要让一整块 UI 整体淡入淡出必须用这个。
    /// 目标物体上没有 CanvasGroup 时返回 null（调用方会跳过这个效果）。
    /// </summary>
    [Serializable]
    public class CanvasGroupFadeEffect : MangaAnimEffect
    {
        private float beforeAlpha = 1f;

        [Label("目标透明度（1 = 不透明，0 = 全透明）")]
        [SerializeField] private float alpha = 1f;
        [Label("时长（秒）")]
        [SerializeField] private float duration = 0.2f;
        [Label("缓动曲线")]
        [SerializeField] private Ease ease = Ease.OutQuad;

        public override Tween CreateTween(Transform target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogWarning($"[动画] {target.name} 上没有 CanvasGroup，CanvasGroupFadeEffect 已跳过");
                return null;
            }

            beforeAlpha = canvasGroup.alpha;
            return WithDelayAndPause(canvasGroup.DOFade(alpha, duration).SetEase(ease));
        }

        public override void ResetAni(Transform target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = beforeAlpha;
            }
        }
    }
}
