using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>立绘换表情时怎么过渡</summary>
    public enum PortraitExpressionTransition
    {
        [InspectorName("直接换（最省，硬切）")]
        Instant = 0,

        [InspectorName("闪一下（先淡到透明再换，有漫画感）")]
        Flash = 1,

        [InspectorName("交叉淡化（要接第二张图；没有就退化成闪一下）")]
        CrossFade = 2,
    }

    /// <summary>
    /// 立绘：分镜里那个「角色画面」。
    ///
    /// ★ 和立绘框（PortraitFrame）是**兄弟节点，不是父子**：
    ///   框的缩放/位移不会影响它，它也不会被框裁掉。框管画格，它管角色。
    ///
    /// ★ 代码**不做任何调整**：位置 / 尺寸 / 缩放 / 比例 / 九宫格 / 是否保比例
    ///   全由你在编辑器里摆（编辑器里看到的就是运行时的效果，调构图不用跑一遍）。
    ///   运行时它只做两件事 —— 换 sprite、按配置播一个"换表情过渡"。
    ///
    /// 所以"不能变形"这件事交给出图规范：
    ///   ① 统一画布（比如 1000×1400 或 2048×2048），角色**脚底对齐画布底边**、头顶留白一致；
    ///   ② 角色在画布里的**高度占比一致** —— 谁高谁矮靠画里表达，不要靠出图尺寸；
    ///   ③ 按模板里立绘框的宽高比出图，这样框多大角色就多大，不需要代码适配；
    ///   ④ 透明 PNG，不带背景、不带框（框是模板里的东西）。
    /// 想在编辑器里让"矩形跟着图的比例走"，自己挂 UGUI 自带的 AspectRatioFitter（所见即所得）。
    ///
    /// 图从哪来：DialogueSpeaker.GetFace(表情)（默认 / 高兴 / 难过 / 惊讶 / 害怕，没配就回退默认）。
    /// 编辑期 Image 上是占位图，运行时被换掉，尺寸不受影响。
    /// </summary>
    public class Portrait : SlotVisual
    {
        [Label("立绘（留空 = 取本物体上的 Image）")]
        public Image image;

        [Header("换表情过渡")]
        [Label("换表情怎么过渡")]
        [SerializeField] private PortraitExpressionTransition expressionTransition = PortraitExpressionTransition.Flash;

        [Label("过渡时长（秒）")]
        [SerializeField] private float expressionDuration = 0.12f;

        [Label("交叉淡化用的第二张图（可空；留空则退化成一闪）")]
        [SerializeField] private Image fadeLayer;

        private Sprite m_CurrentSprite;

        /// <summary>上一次演的是谁：只有"同一个人换表情"才值得做过渡</summary>
        private DialogueSpeaker m_LastSpeaker;

        private Tween m_ExpressionTween;

        protected override void Awake()
        {
            base.Awake();

            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (image != null)
            {
                m_CurrentSprite = image.sprite;
            }

            if (fadeLayer != null)
            {
                fadeLayer.enabled = false;      // 只在过渡的那一瞬间用
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            m_ExpressionTween?.Kill();
            m_ExpressionTween = null;
        }

        /// <summary>换画：null = 这个位置没人（图隐藏）。只换 sprite，别的什么都不碰</summary>
        public void SetArt(Sprite sprite)
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null && Speaker != null;
            m_CurrentSprite = sprite;
        }

        /// <summary>现在这张画（调试用）</summary>
        public Sprite Art => image != null ? image.sprite : null;

        protected override void OnSpeakerChanged(DialogueSpeaker speaker, DialogueCharExpressionEnum expression)
        {
            var next = speaker != null ? speaker.GetFace(expression) : null;

            // 是不是"同一个人换了张脸"：这才是过渡要管的场景
            bool sameSpeaker = speaker != null && speaker == m_LastSpeaker;
            m_LastSpeaker = speaker;

            // 什么时候不做过渡：
            //   · 不同的人 / 清空 / 入场就位 —— 本来就该直接换
            //   · 图没变（同一张）
            //   · 入场动画还在播（那时立绘正从无到有，再叠一层过渡只会乱）
            if (!sameSpeaker || !CanTransitionTo(next))
            {
                KillExpressionTween();
                SetArt(next);
                return;
            }

            PlayExpressionTransition(next);
        }

        private bool CanTransitionTo(Sprite next)
        {
            if (expressionTransition == PortraitExpressionTransition.Instant)
            {
                return false;
            }

            if (IsEntering || image == null || !image.enabled)
            {
                return false;
            }

            return next != null && next != m_CurrentSprite && m_CurrentSprite != null;
        }

        private void PlayExpressionTransition(Sprite next)
        {
            KillExpressionTween();

            float half = Mathf.Max(0.01f, expressionDuration * 0.5f);

            if (expressionTransition == PortraitExpressionTransition.CrossFade && fadeLayer != null)
            {
                // 交叉淡化：旧图放到 fadeLayer 上淡出，主图直接换成新图淡入
                fadeLayer.sprite = m_CurrentSprite;
                fadeLayer.enabled = true;

                var fadeColor = fadeLayer.color;
                fadeColor.a = 1f;
                fadeLayer.color = fadeColor;

                SetArt(next);

                var from = image.color;
                var to = from;
                to.a = 1f;
                image.color = new Color(from.r, from.g, from.b, 0f);

                m_ExpressionTween = DOTween.Sequence()
                    .Join(fadeLayer.DOFade(0f, expressionDuration).SetEase(Ease.OutQuad))
                    .Join(image.DOFade(1f, expressionDuration).SetEase(Ease.OutQuad))
                    .OnComplete(() =>
                    {
                        fadeLayer.enabled = false;
                        var color = image.color;
                        color.a = 1f;
                        image.color = color;
                    });
                return;
            }

            // 闪一下：淡到透明 → 换图 → 淡回来（漫画里"啪"一下换脸的观感）
            m_ExpressionTween = DOTween.Sequence()
                .Append(image.DOFade(0f, half).SetEase(Ease.InQuad))
                .AppendCallback(() => SetArt(next))
                .Append(image.DOFade(1f, half).SetEase(Ease.OutQuad));
        }

        private void KillExpressionTween()
        {
            m_ExpressionTween?.Kill();
            m_ExpressionTween = null;

            if (image != null)
            {
                var color = image.color;
                color.a = 1f;
                image.color = color;
            }

            if (fadeLayer != null)
            {
                fadeLayer.enabled = false;
            }
        }

        protected override void OnHighlightColor(bool isSpeaker)
        {
            if (image != null)
            {
                image.color = isSpeaker ? normalColor : dimColor;
            }
        }
    }
}
