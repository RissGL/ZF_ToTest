using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>气泡从锚点往哪个方向长（决定 RectTransform 的 pivot）</summary>
    public enum BubbleGrowDirection
    {
        RightUp,
        LeftUp,
        RightDown,
        LeftDown,
        Up,
        Down,
    }

    /// <summary>
    /// 单个气泡。
    /// 一般由 BubbleStage 调用，不要直接调 Play/Replay/Close。
    ///
    /// 位置怎么定：
    ///   RectTransform 的 anchoredPosition 由你在模板 Prefab 里拖（这就是气泡的"锚点"），
    ///   pivot 由 grow 决定 → 气泡尺寸随内容变化时，是朝锚点反方向"长大"的，
    ///   尾巴尖始终贴在说话者那边，不会因为换句长短不同而跳位。
    ///
    /// 三个自由度：
    ///   位置 = 模板里拖        （同一格内固定）
    ///   大小 = 按文本量出来    （每句都不同）
    ///   样式 = request.style   （每句可选）
    /// </summary>
    public class Bubble : VisualShowBase
    {
        [Header("身份")]
        [Label("key（谁的气泡；留空 = 通用兜底，谁都能用）")]
        public string key;

        [Label("从锚点往哪长")]
        public BubbleGrowDirection grow = BubbleGrowDirection.RightUp;

        [Label("默认样式（请求里没指定样式时用这个）")]
        public BubbleStyleSO defaultStyle;

        [Header("引用")]
        [Label("气泡框（Image，挂在本物体上）")]
        public Image frame;

        [Label("名字（可空）")]
        public TMP_Text nameText;

        [Label("正文（可空）")]
        public TMP_Text bodyText;

        [Label("继续箭头 ▼（可空）")]
        public GameObject arrow;


        [Label("打字机（挂在本物体或子物体上）")]
        public TypewriterText typewriter;

        /// <summary>这一句打完了</summary>
        public event Action<Bubble> Typed;

        /// <summary>收起了（退场动画播完）</summary>
        public event Action<Bubble> Closed;

        public string Key => key ?? string.Empty;

        /// <summary>逻辑上是不是显示中（点 Close 之后立刻变 false，退场动画还在播）</summary>
        public bool IsVisible { get; private set; }

        /// <summary>退场重弹中（旧文本收场、新一句还没弹的空档）；Stage.IsTyping 把它算进去，防连点跳句</summary>
        public bool IsTransitioning { get; private set; }

        public bool IsTyping => typewriter != null && typewriter.IsPlaying;

        private RectTransform m_Rect;
        private BubbleStyleSO m_Style;
        private BubbleAnimSet m_AnimSet;

        /// <summary>退场播完要重弹的那句；Play/Close 会把它作废（置 null），防止过期回调弹出旧文本</summary>
        private BubbleRequest? m_PendingReplay;

        /// <summary>"这个气泡没配入场动画"只提醒一次，别每句都刷</summary>
        private bool m_WarnedNoEnterAnimation;


        private RectTransform Rect
        {
            get
            {
                if (m_Rect == null)
                {
                    m_Rect = transform as RectTransform;
                }
                return m_Rect;
            }
        }

        protected override void Awake()
        {
            base.Awake();       // 收起态 + group / 基准缩放都在基类里初始化

            if (frame == null)
            {
                frame = GetComponent<Image>();
            }
            if (typewriter == null)
            {
                typewriter = GetComponentInChildren<TypewriterText>(true);
            }

            ApplyGrow();
            SetVisible(false);
        }

        private void OnValidate()
        {
            // 在 Inspector 里改 grow 就能立刻看到 pivot 变化
            ApplyGrow();
        }

        // ===================== 由 BubbleStage 调用 =====================

        /// <summary>弹出并开始说话</summary>
        public void Play(BubbleRequest request)
        {
            gameObject.SetActive(true);

            // 有还在等退场重弹的旧请求 → 作废，最新这句话说了算
            m_PendingReplay = null;
            IsTransitioning = false;

            // 强制回到"收起状态"：这样即使退场动画没做镜像，下次弹出也一定有效果
            SetVisible(false);

            ApplyStyle(request.style);
            ApplyText(request.displayName, request.text);
            SetArrow(false);

            // 入场动画和打字同时开始（气泡弹出来的同时字已经在走，手感更紧）
            PlayEnterEffects();
            StartTyping(request);

            IsVisible = true;
            PlayAudio(m_AnimSet != null ? m_AnimSet.enterAudio : null);
        }

        /// <summary>
        /// 同一个人接着说下一句：旧文本先收场（播退场动画），收完再完整弹出新的一句。
        /// 不是原地换字 —— 每一句都是一次完整的「消失 / 弹出」。
        /// </summary>
        public void Replay(BubbleRequest request)
        {
            if (!IsVisible)
            {
                Play(request);           // 已经收着的：直接弹
                return;
            }

            typewriter?.Cancel();
            SetArrow(false);
            IsVisible = false;
            IsTransitioning = true;
            m_PendingReplay = request;

            PlayAudio(m_AnimSet?.exitAudio);
            PlayExit(ExitEffects(), null, () =>
            {
                var replay = m_PendingReplay;
                m_PendingReplay = null;
                IsTransitioning = false;

                if (replay != null)
                {
                    // 中途被 Play/Close 顶掉时这里是 null（那边自己会弹最新的），别把旧句弹出来
                    Play(replay.Value);
                }
            });
        }

        /// <summary>收起（退场动画播完事件里会再发 Closed）</summary>
        public void Close()
        {
            m_PendingReplay = null;      // 等退场重弹的请求作废
            IsTransitioning = false;

            typewriter?.Cancel();
            SetArrow(false);
            IsVisible = false;

            PlayAudio(m_AnimSet?.exitAudio);

            PlayExit(ExitEffects(), null, () =>
            {
                gameObject.SetActive(false);
                Closed?.Invoke(this);
            });
        }

        /// <summary>立刻补全这一句（玩家点「继续」）</summary>
        public void CompleteTyping()
        {
            typewriter?.Complete();
        }

        // ===================== 内部 =====================

        /// <summary>
        /// 按 grow 摆好 pivot（Awake / OnValidate 会自动调）。
        /// ★ 编辑器工具（搭建器）配好 grow 之后也要立刻调一次：
        ///   pivot 决定「锚点钉在气泡的哪个角」，Prefab 里拖位置时如果 pivot 还是中心，
        ///   运行时一改 pivot 气泡会偏半个身位 —— 拖的和跑的对不上，模板就没法调了。
        /// </summary>
        public void ApplyGrow()
        {
            if (Rect == null)
            {
                return;
            }

            switch (grow)
            {
                case BubbleGrowDirection.RightUp: Rect.pivot = new Vector2(0f, 0f); break;
                case BubbleGrowDirection.LeftUp: Rect.pivot = new Vector2(1f, 0f); break;
                case BubbleGrowDirection.RightDown: Rect.pivot = new Vector2(0f, 1f); break;
                case BubbleGrowDirection.LeftDown: Rect.pivot = new Vector2(1f, 1f); break;
                case BubbleGrowDirection.Up: Rect.pivot = new Vector2(0.5f, 0f); break;
                default: Rect.pivot = new Vector2(0.5f, 1f); break;
            }
        }

        private void SetVisible(bool visible)
        {
            if (visible)
            {
                ApplyVisibleState();
            }
            else
            {
                // 收起态（缩放倍数 / 透明度）统一由 VisualShowBase 处理
                ApplyHiddenState();
                SetArrow(false);
            }
        }

        /// <summary>
        /// 播入场动画，并把「动画没管到的属性」直接摆到可见状态。
        ///
        /// ★ 为什么必须有这一步：Play 之前强制回到了收起态（alpha = hiddenAlpha），
        ///   而把 alpha 拉回 1 的责任全在入场效果里的 CanvasGroupFadeEffect 上。
        ///   只要样式没挂 animSet、或者 animSet 里没配淡入，气泡就会永远 alpha = 0：
        ///   音效照响、打字机照跑，屏幕上一个气泡都没有 —— 这个坑太隐蔽，这里兜住并报出来。
        /// </summary>
        private void PlayEnterEffects()
        {
            var effects = EnterEffects();

            // 收起态 → 播效果 → 没管到 alpha/scale 就兜底（都在 VisualShowBase 里）
            PlayEnter(effects, null, null);


            if (effects == null || effects.Count == 0)
            {
                if (!m_WarnedNoEnterAnimation)
                {
                    m_WarnedNoEnterAnimation = true;
                    Debug.LogWarning($"[气泡]「{name}」没有入场动画（入场效果是空的），已直接显示。\n" +
                                     "   常见原因：这句用的 BubbleStyleSO 没挂 animSet，或 animSet 的入场效果没配。\n" +
                                     "   （要淡入就加 CanvasGroupFadeEffect alpha=1，要弹出来就加 ScaleEffect targetScale=(1,1,1)）");
                }
            }
        }

        private void ApplyStyle(BubbleStyleSO style)
        {
            m_Style = style != null ? style : defaultStyle;
            m_AnimSet = m_Style != null ? m_Style.animSet : null;

            if (m_Style == null)
            {
                return;
            }

            if (frame != null && m_Style.frameSprite != null)
            {
                frame.sprite = m_Style.frameSprite;
            }

            if (bodyText != null)
            {
                bodyText.fontSize = m_Style.fontSize;
                bodyText.color = m_Style.textColor;
            }

            if (nameText != null)
            {
                nameText.fontSize = m_Style.nameFontSize;
                nameText.color = m_Style.nameColor;
            }
        }

        private void ApplyText(string displayName, string text)
        {
            bool showName = m_Style == null || m_Style.showName;

            if (nameText != null)
            {
                nameText.gameObject.SetActive(showName && !string.IsNullOrEmpty(displayName));
                nameText.text = displayName ?? string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = text ?? string.Empty;
            }

            Layout();
        }

        /// <summary>按文本量出气泡大小（pivot 不动 → 朝锚点反方向长）</summary>
        public void Layout()
        {
            if (Rect == null || bodyText == null || m_Style == null)
            {
                return;
            }

            float maxWidth = Mathf.Max(40f, m_Style.maxTextWidth);

            Vector2 bodySize = bodyText.GetPreferredValues(bodyText.text, maxWidth, 0f);
            float textWidth = Mathf.Min(bodySize.x, maxWidth);

            float nameHeight = 0f;
            if (nameText != null && nameText.gameObject.activeSelf)
            {
                nameHeight = nameText.GetPreferredValues(nameText.text, maxWidth, 0f).y + m_Style.nameSpacing;
            }

            Rect.sizeDelta = new Vector2(
                textWidth + m_Style.padX * 2f,
                bodySize.y + nameHeight + m_Style.padY * 2f);
        }

        private void StartTyping(BubbleRequest request)
        {
            float speed = request.speedOverride > 0f
                ? request.speedOverride
                : m_AnimSet != null ? m_AnimSet.typingSpeed : 0.035f;

            float pause = m_AnimSet != null ? m_AnimSet.punctuationPause : 0.14f;
            string pauseChars = m_AnimSet != null ? m_AnimSet.pauseChars : null;

            if (typewriter == null)
            {
                FinishTyping(request);
                return;
            }

            typewriter.Play(bodyText != null ? bodyText.text : string.Empty, speed, pause, pauseChars,
                () => FinishTyping(request));
        }

        private void FinishTyping(BubbleRequest request)
        {
            SetArrow(m_Style == null || m_Style.showArrow);
            request.onTyped?.Invoke();
            Typed?.Invoke(this);
        }

        private void SetArrow(bool show)
        {
            if (arrow != null)
            {
                arrow.SetActive(show);
            }
        }

        private List<MangaAnimEffect> EnterEffects() => m_AnimSet != null ? m_AnimSet.enterEffects : null;
        private List<MangaAnimEffect> ExitEffects() => m_AnimSet != null ? m_AnimSet.exitEffects : null;

    }
}
