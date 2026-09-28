using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 条纹划入擦除（**纯 UI，不需要 shader**）：整屏切成 N 条，每条错开一点时间刷进来盖满，
    /// 再错开一点时间刷出去 —— 漫画里那种"刷！一下翻页"的感觉。
    ///
    /// 层级（挂在场景里，**不要放进模板 Prefab**）：
    ///   Canvas
    ///     ├─ DialogueTemplateHost        ← 模板实例会被隐藏/切换
    ///     └─ BarsLayer                   ← 本组件（放在模板之后，才画得在模板上面）
    ///          └─ BarRoot                ← 条的容器（按方向整体旋转，条在里面沿本地 X 移动）
    ///               └─ Bar_0             ← 第一条：**同时当模板**，其余条在运行时按它拷贝
    ///
    /// 和"遮罩平移"的区别：那块是一整张纸扫过去（干净利落），这个是分条错时刷过（有节奏感）。
    ///
    /// 参数全在 Inspector：条数 / 方向 / 错开间隔 / 每段时长 / 缓动 / 条色 / 音效 / 是否中间先动。
    /// </summary>
    public class BarsWipeTransition : MonoBehaviour, IDialogueTransition, IDialogueTransitionStyle
    {
        [Header("引用")]
        [Label("条的容器（按方向整体旋转；条都挂在它下面）")]
        [SerializeField] private RectTransform barRoot;

        [Label("第一条（当模板用，运行时按它拷出其余条；它自己也参与）")]
        [SerializeField] private RectTransform barTemplate;

        [Label("整体隐藏用的 CanvasGroup（空 = 自动取本物体）")]
        [SerializeField] private CanvasGroup group;

        [Header("外观")]
        [Label("条数")]
        [Range(2, 32)]
        [SerializeField] private int barCount = 8;

        [Label("方向（条从哪边刷进来）")]
        [SerializeField] private WipeDirection direction = WipeDirection.LeftToRight;

        [Label("条的颜色")]
        [SerializeField] private Color barColor = new Color(0.06f, 0.06f, 0.09f, 1f);

        [Label("中间先动（关 = 从一边依次刷过去）")]
        [SerializeField] private bool fromCenter = false;

        [Header("节奏")]
        [Label("每条错开多少秒")]
        [SerializeField] private float stagger = 0.045f;

        [Label("每条刷进来 / 刷出去各多少秒")]
        [SerializeField] private float segmentDuration = 0.28f;

        [Label("缓动曲线")]
        [SerializeField] private Ease ease = Ease.OutQuad;

        [Header("音效")]
        [Label("擦除音效（可空）")]
        [SerializeField] private ShowAudioEventSO wipeAudio;

        /// <summary>画布对角线之外的余量，保证条一定盖得住</summary>
        private const float Margin = 8f;

        private readonly List<RectTransform> m_Bars = new List<RectTransform>();
        private Sequence m_Seq;
        private bool m_Playing;

        /// <summary>组件上配的默认方向 / 颜色：组数据没覆盖时用它（别被上一次的覆盖值污染）</summary>
        private WipeDirection m_DefaultDirection;
        private Color m_DefaultColor;

        /// <summary>组件上配的默认时长（组数据没覆盖时用它）</summary>
        private float m_DefaultDuration;

        public bool IsPlaying => m_Playing;

        private void Awake()
        {
            m_DefaultDirection = direction;
            m_DefaultColor = barColor;
            m_DefaultDuration = segmentDuration;

            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (barRoot == null || barTemplate == null)
            {
                Debug.LogError("[对话擦除] BarsWipeTransition 没挂「条的容器」或「第一条」", this);
                return;
            }

            BuildBars();
            DisableRaycast();
            Hide();
        }

        private void OnDestroy()
        {
            m_Seq?.Kill();
            m_Seq = null;
        }

        // ===================== IDialogueTransition =====================

        public void Play(Action onCovered, Action onFinished)
        {
            if (barRoot == null || barTemplate == null || m_Bars.Count == 0)
            {
                // 没配好就别把流程卡住：直接切
                onCovered?.Invoke();
                onFinished?.Invoke();
                return;
            }

            m_Seq?.Kill();
            m_Playing = true;

            float size = GetSize();
            float strip = size / m_Bars.Count;

            PrepareBars(size, strip);
            Show();

            wipeAudio?.Play();

            float maxDelay = GetMaxDelay();
            float inDuration = Mathf.Max(0.01f, segmentDuration);
            float coveredAt = maxDelay + inDuration;
            float finishedAt = coveredAt + maxDelay + inDuration;

            m_Seq = DOTween.Sequence();

            for (int i = 0; i < m_Bars.Count; i++)
            {
                var bar = m_Bars[i];
                float delay = GetDelay(i);

                bar.anchoredPosition = new Vector2(-size, bar.anchoredPosition.y);   // 画外
                m_Seq.Insert(delay, bar.DOAnchorPosX(0f, inDuration).SetEase(ease)); // 到位 = 盖住自己这一条
            }

            m_Seq.InsertCallback(coveredAt, () => onCovered?.Invoke());

            for (int i = 0; i < m_Bars.Count; i++)
            {
                var bar = m_Bars[i];
                float delay = GetDelay(i);

                m_Seq.Insert(coveredAt + delay, bar.DOAnchorPosX(size, inDuration).SetEase(ease));  // 继续刷出画面
            }

            m_Seq.InsertCallback(finishedAt, () =>
            {
                m_Playing = false;
                Hide();
                onFinished?.Invoke();
            });
        }

        public void Stop()
        {
            m_Seq?.Kill();
            m_Seq = null;
            m_Playing = false;
            Hide();
        }

        /// <summary>组数据传进来的方向 / 颜色 / 时长（没填的项退回组件上配的默认值；时长指每段刷进/刷出的时长）</summary>
        public void ApplyStyle(DialogueTransitionStyle style)
        {
            direction = style.HasDirection ? style.Direction : m_DefaultDirection;
            barColor = style.HasColor ? style.Color : m_DefaultColor;
            segmentDuration = style.HasDuration ? style.Duration : m_DefaultDuration;
        }

        // ===================== 内部 =====================

        /// <summary>按第一条拷出其余条（只做一次，之后复用）</summary>
        private void BuildBars()
        {
            m_Bars.Clear();
            m_Bars.Add(barTemplate);

            int want = Mathf.Max(2, barCount);

            for (int i = m_Bars.Count; i < want; i++)
            {
                var clone = Instantiate(barTemplate, barRoot);
                clone.name = $"{barTemplate.name}_{i}";
                m_Bars.Add(clone);
            }

            // 多出来的（把条数调小之后）藏起来，别留一堆僵尸条在画面上
            for (int i = want; i < barRoot.childCount; i++)
            {
                var child = barRoot.GetChild(i) as RectTransform;
                if (child != null && !m_Bars.Contains(child))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>按画布算尺寸/朝向：整屏切成 N 条，条沿本地 X 刷过（容器负责旋转）</summary>
        private void PrepareBars(float size, float strip)
        {
            barRoot.anchorMin = new Vector2(0.5f, 0.5f);
            barRoot.anchorMax = new Vector2(0.5f, 0.5f);
            barRoot.pivot = new Vector2(0.5f, 0.5f);
            barRoot.localScale = Vector3.one;
            barRoot.localRotation = Quaternion.Euler(0f, 0f, WipeDirectionUtil.ToAngle(direction));
            barRoot.sizeDelta = new Vector2(size, size);

            int count = m_Bars.Count;

            for (int i = 0; i < count; i++)
            {
                var bar = m_Bars[i];

                bar.gameObject.SetActive(true);
                bar.anchorMin = new Vector2(0.5f, 0.5f);
                bar.anchorMax = new Vector2(0.5f, 0.5f);
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.localScale = Vector3.one;
                bar.localRotation = Quaternion.identity;
                bar.sizeDelta = new Vector2(size, strip);

                // 条沿本地 Y 排开（穿过方向 = 本地 X）
                float y = -size * 0.5f + strip * (i + 0.5f);
                bar.anchoredPosition = new Vector2(0f, y);

                if (bar.TryGetComponent<Graphic>(out var graphic))
                {
                    graphic.color = barColor;
                }
            }
        }

        private float GetSize()
        {
            var parentRect = barRoot.parent as RectTransform;
            float width = parentRect != null ? parentRect.rect.width : 1920f;
            float height = parentRect != null ? parentRect.rect.height : 1080f;
            return Mathf.Sqrt(width * width + height * height) + Margin;
        }

        /// <summary>第 i 条什么时候开始动（中间先动 = 从中心往两边排时间）</summary>
        private float GetDelay(int index)
        {
            if (!fromCenter)
            {
                return stagger * index;
            }

            float center = (m_Bars.Count - 1) * 0.5f;
            return stagger * Mathf.Abs(index - center);
        }

        private float GetMaxDelay()
        {
            float max = 0f;
            for (int i = 0; i < m_Bars.Count; i++)
            {
                max = Mathf.Max(max, GetDelay(i));
            }

            return max;
        }

        private void Show()
        {
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            transform.SetAsLastSibling();
        }

        private void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
            }
        }

        private void DisableRaycast()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
