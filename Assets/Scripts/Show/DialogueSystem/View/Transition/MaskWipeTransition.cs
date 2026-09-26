using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 遮罩平移擦除（**纯 UI，不需要 shader**）：一张"纸"从画外扫进来盖满整屏，
    /// 再继续朝同一个方向扫出画面 —— 观众看到的就是"旧的一格被擦掉、新的擦出来"。
    ///
    /// 层级（必须挂在场景里、**不要放进模板 Prefab**）：
    ///   Canvas
    ///     ├─ DialogueTemplateHost      ← 模板实例会被隐藏/切换
    ///     └─ WipeLayer                 ← 本组件（放在模板之后，才画得在模板上面）
    ///          └─ Paper               ← 纸：一块纯色/网点 Image（尺寸由代码按画布算，随便摆）
    ///               └─ Edge          ← 前缘：撕纸边/斜边（挂在纸下面，跟着纸一起走）
    ///
    /// 数学（就一句）：纸做成"边长 = 画布对角线"的正方形并居中，那么它**任何角度都能盖满整屏**；
    /// 中心从 -对角线 推到 0（盖满）再推到 +对角线（推出画面），两端都在画外。
    /// 纸整体旋转成"本地 +X = 扫过去的方向"，所以前缘永远贴在本地 +X 那一侧，换方向不用重摆前缘。
    ///
    /// 参数全在 Inspector 上：方向 / 时长 / 缓动 / 前缘宽度 / 纸色 / 音效 —— 拖到满意即可。
    /// </summary>
    public class MaskWipeTransition : MonoBehaviour, IDialogueTransition, IDialogueTransitionStyle
    {
        [Header("引用")]
        [Label("纸（盖住用的那块 Image；尺寸由代码按画布算，编辑器里摆哪都行）")]
        [SerializeField] private RectTransform cover;

        [Label("前缘贴图（可空；要挂在「纸」下面，才会跟着纸一起扫）")]
        [SerializeField] private RectTransform edge;

        [Label("整体隐藏用的 CanvasGroup（空 = 自动取本物体；也没有就靠移出画面隐藏）")]
        [SerializeField] private CanvasGroup group;

        [Header("外观")]
        [Label("方向（纸从哪边扫进来）")]
        [SerializeField] private WipeDirection direction = WipeDirection.LeftToRight;

        [Label("纸的颜色（网点/纸纹直接做在纸上，这里只是底色）")]
        [SerializeField] private Color coverColor = new Color(0.06f, 0.06f, 0.09f, 1f);

        [Label("前缘宽度（0 = 不要前缘，硬边擦）")]
        [SerializeField] private float edgeWidth = 0f;

        [Header("节奏")]
        [Label("总时长（秒）—— 盖住和露出各占一半")]
        [SerializeField] private float duration = 0.55f;

        [Label("缓动曲线")]
        [SerializeField] private Ease ease = Ease.InOutQuad;

        [Label("推迟多少秒再开始（0 = 立刻）")]
        [SerializeField] private float startDelay = 0f;

        [Header("音效")]
        [Label("擦除音效（可空）")]
        [SerializeField] private ShowAudioEventSO wipeAudio;

        /// <summary>画布对角线之外的余量，保证纸一定盖得住</summary>
        private const float Margin = 8f;

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
            m_DefaultColor = coverColor;
            m_DefaultDuration = duration;

            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (cover == null)
            {
                Debug.LogError("[对话擦除] MaskWipeTransition 没挂「纸」，这次只能直接切", this);
                return;
            }

            DisableRaycast(cover);
            DisableRaycast(edge);

            if (edge != null && edge.parent != cover)
            {
                Debug.LogWarning("[对话擦除] 前缘不是「纸」的子物体，它不会跟着纸一起扫" +
                                 "（把前缘拖到纸下面即可）", this);
            }

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
            if (cover == null)
            {
                // 没配纸就别把流程卡住：直接切
                onCovered?.Invoke();
                onFinished?.Invoke();
                return;
            }

            m_Seq?.Kill();
            m_Playing = true;

            var parentRect = cover.parent as RectTransform;
            float width = parentRect != null ? parentRect.rect.width : 1920f;
            float height = parentRect != null ? parentRect.rect.height : 1080f;

            float size = Mathf.Sqrt(width * width + height * height) + Margin;
            float distance = size;                      // 中心推到 ±对角线 = 两端都在画外
            Vector2 axis = WipeDirectionUtil.ToAxis(direction);
            Vector2 from = -axis * distance;
            Vector2 to = axis * distance;

            PrepareCover(size);
            ApplyColor();
            Show();

            wipeAudio?.Play();

            float half = Mathf.Max(0.01f, duration * 0.5f);

            m_Seq = DOTween.Sequence();
            m_Seq.AppendInterval(startDelay);

            cover.anchoredPosition = from;
            m_Seq.Append(cover.DOAnchorPos(Vector2.zero, half).SetEase(ease));
            m_Seq.AppendCallback(() => onCovered?.Invoke());

            m_Seq.Append(cover.DOAnchorPos(to, half).SetEase(ease));
            m_Seq.AppendCallback(() =>
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
            Hide();         // 打断时不动画面状态，直接收干净
        }

        /// <summary>组数据传进来的方向 / 颜色 / 时长（没填的项退回组件上配的默认值）</summary>
        public void ApplyStyle(DialogueTransitionStyle style)
        {
            direction = style.HasDirection ? style.Direction : m_DefaultDirection;
            coverColor = style.HasColor ? style.Color : m_DefaultColor;
            duration = style.HasDuration ? style.Duration : m_DefaultDuration;
        }

        // ===================== 内部 =====================

        /// <summary>按画布算纸的尺寸/朝向/锚点（这一块是动画几何，不算"改美术摆的布局"）</summary>
        private void PrepareCover(float size)
        {
            cover.anchorMin = new Vector2(0.5f, 0.5f);
            cover.anchorMax = new Vector2(0.5f, 0.5f);
            cover.pivot = new Vector2(0.5f, 0.5f);
            cover.localScale = Vector3.one;
            cover.localRotation = Quaternion.Euler(0f, 0f, WipeDirectionUtil.ToAngle(direction));
            cover.sizeDelta = new Vector2(size, size);

            if (edge == null)
            {
                return;
            }

            // 前缘贴在纸的本地 +X 边（= 扫过去的前锋），换方向不用重摆
            edge.anchorMin = new Vector2(0.5f, 0.5f);
            edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.localRotation = Quaternion.identity;
            edge.sizeDelta = new Vector2(edgeWidth, size);
            edge.anchoredPosition = new Vector2(size * 0.5f - edgeWidth * 0.5f, 0f);
            edge.gameObject.SetActive(edgeWidth > 0f);
        }

        private void ApplyColor()
        {
            if (cover.TryGetComponent<Graphic>(out var graphic))
            {
                graphic.color = coverColor;
            }
        }

        private void Show()
        {
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;       // 别挡选项 / 继续按钮的点击
                group.interactable = false;
            }

            // 保证画在最上面：换模板会动兄弟顺序，擦除层要始终压在上面
            transform.SetAsLastSibling();
        }

        private void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
            }
        }

        private static void DisableRaycast(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
