using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 「会演出的可视部件」的基类：统一管**收起状态 / 进出场播放 / 没配动画时的兜底**。
    ///
    /// 继承它的两套东西：
    ///   SlotVisual（框 / 立绘 / 背景）—— 槽里的角色部件，多了"说话人高亮"
    ///   Bubble                      —— 气泡，多了排版 / 打字 / 顶掉重弹
    /// 之前这两套各写了一遍同样的逻辑，同一个 bug（比如"没配淡入导致永远透明"）
    /// 要在两处各修一遍 —— 现在只在这里一处。
    ///
    /// ★ 缩放是**相对**的：`hiddenScale` 是倍数（默认 1 = 不改），乘在你在编辑器里摆的缩放上，
    ///   收起 / 兜底都不会把你的缩放覆盖成别的值。
    ///   ★ 走 Transform 还是 RectTransform 由 `hiddenScaleMode` 决定：
    ///     界面部件（框 / 立绘 / 背景 / 选项面板）选 **Rect** —— 缩的是矩形尺寸，九宫格边框和字号不会变形。
    ///     选了 Rect 就把 `hiddenScale` 留成 1，"从小弹出来"交给动画的「用起始缩放」：
    ///     否则收起态先改了尺寸，动画又按当前尺寸算倍数，会缩两次。
    ///
    /// ★ 播放统一走 <see cref="EffectSequencePlayer"/>：
    ///   先 kill 上一次 → 拼一条序列 → 没效果就立刻回调。
    /// </summary>
    public abstract class VisualShowBase : MonoBehaviour
    {
        [Header("引用")]
        [Label("整体淡入淡出用的 CanvasGroup（挂在本物体上）")]
        public CanvasGroup group;

        [Header("收起状态（每次入场前会先回到这里；都是倍数，不覆盖你摆的值）")]
        [Label("缩放作用对象（收起 / 高亮 / 兜底共用；Rect = 改 RectTransform 尺寸，界面推荐）")]
        public VisualScaleMode hiddenScaleMode;

        [Label("收起时的缩放（默认 1 = 不改）")]
        public Vector3 hiddenScale = Vector3.one;

        [Label("收起时的透明度（默认 0 = 完全隐藏）")]
        [Range(0f, 1f)]
        public float hiddenAlpha = 0f;

        /// <summary>入场动画还在播</summary>
        public bool IsEntering { get; private set; }

        /// <summary>本物体在编辑器里摆的缩放（收起 / 兜底都基于它算，绝不覆盖它）</summary>
        protected Vector3 BaseScale { get; private set; } = Vector3.one;

        /// <summary>本物体在编辑器里摆的尺寸（Rect 模式下的收起 / 兜底基于它算，绝不覆盖它）</summary>
        protected Vector2 BaseSize { get; private set; }

        private Tween m_CurrentTween;

        protected virtual void Awake()
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            BaseScale = transform.localScale;

            if (transform is RectTransform rect)
            {
                BaseSize = rect.sizeDelta;
            }

            ApplyHiddenState();
        }

        protected virtual void OnDestroy()
        {
            KillCurrent();
        }

        // ===================== 状态 =====================

        /// <summary>回到收起状态：缩放按倍数乘、透明度设为 hiddenAlpha</summary>
        protected void ApplyHiddenState()
        {
            ApplyScale(hiddenScale);

            if (group != null)
            {
                group.alpha = hiddenAlpha;
            }
        }

        /// <summary>直接摆到"可见"状态（没配动画时的兜底）</summary>
        protected void ApplyVisibleState()
        {
            ApplyScale(Vector3.one);

            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        /// <summary>
        /// 按倍数调大小（倍数 = 1 就是回到美术摆的原样）。
        /// ★ 走 Transform 还是 RectTransform 由 <see cref="hiddenScaleMode"/> 决定：
        ///   界面部件用 Rect —— 缩的是矩形尺寸，九宫格边框厚度和字号都不会被缩变形。
        /// </summary>
        protected void ApplyScale(Vector3 factor)
        {
            if (hiddenScaleMode == VisualScaleMode.Rect && transform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(BaseSize.x * factor.x, BaseSize.y * factor.y);
                return;
            }

            transform.localScale = Multiply(BaseScale, factor);
        }

        /// <summary>丢掉当前动画（来不及播完就要换状态时用）</summary>
        protected void KillCurrent()
        {
            m_CurrentTween?.Kill();
            m_CurrentTween = null;
            IsEntering = false;
        }

        // ===================== 播放 =====================

        /// <summary>
        /// 播入场：先回到收起态 → 播效果 → **动画没管到的属性直接摆到可见** → 回调。
        /// 那个兜底是必须的：否则"样式/动画集里没配淡入"就会得到一个永远透明的东西
        /// （气泡、选项面板都踩过这个坑）。
        /// </summary>
        protected void PlayEnter(List<MangaAnimEffect> effects, List<SlotAnimGroup> groups, Action onDone)
        {
            ApplyHiddenState();
            PlayAudio(GetEnterAudio());

            EffectSequencePlayer.ScanHandled(transform, effects, groups,
                out bool handlesAlpha, out bool handlesScale);

            IsEntering = true;
            EffectSequencePlayer.Play(ref m_CurrentTween, transform, effects, groups, () =>
            {
                IsEntering = false;
                onDone?.Invoke();
            });

            if (!handlesAlpha && group != null)
            {
                group.alpha = 1f;
            }
            if (!handlesScale)
            {
                ApplyScale(Vector3.one);
            }
        }

        /// <summary>播退场：没配效果就直接回调（= 瞬间消失）</summary>
        protected void PlayExit(List<MangaAnimEffect> effects, List<SlotAnimGroup> groups, Action onDone)
        {
            PlayAudio(GetExitAudio());
            EffectSequencePlayer.Play(ref m_CurrentTween, transform, effects, groups, onDone);
        }

        // ===================== 给子类 =====================

        protected virtual ShowAudioEventSO GetEnterAudio() => null;

        protected virtual ShowAudioEventSO GetExitAudio() => null;

        protected void PlayAudio(ShowAudioEventSO audio)
        {
            audio?.Play();
        }

        /// <summary>两个缩放逐分量相乘（把"倍数"作用在美术摆的缩放上）</summary>
        protected static Vector3 Multiply(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }
    }
}
