using DG.Tweening;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.EraGallery
{
    /// <summary>
    /// 世界里的一个时代窗口。
    /// 它只管「长什么样」和「自己的取景框有多大」，不管能不能进、也不碰相机 —— 那些由 EraWorldController 驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public class EraWindow : MonoBehaviour
    {
        [Header("时代设定")]
        [SerializeField] private EraId era = EraId.Stone;

        [Label("时代名（暂时只进日志，有字体后再挂到窗口上）")]
        [SerializeField] private string title = "石器时代";

        [Label("年代")]
        [SerializeField] private string timeline = "公元前 10000";

        [Label("主题色")]
        [SerializeField] private Color theme = Color.white;

        [Header("渲染引用（Tools/时代窗口/搭建四个时代窗口场景 会自动填）")]
        [Label("遮光板：进到这个时代时打开，挡住旁边三个窗口")]
        [SerializeField] private SpriteRenderer backdrop;

        [Label("内容块（点击区域按它 + 边框来算）")]
        [SerializeField] private SpriteRenderer contentRenderer;

        [Label("边框（四条边）")]
        [SerializeField] private SpriteRenderer[] frameParts = new SpriteRenderer[0];

        [Label("跟随状态变暗的东西：内容块 / 地景 / 时代图案 / 序号点")]
        [SerializeField] private SpriteRenderer[] stateTinted = new SpriteRenderer[0];

        [Label("锁住标记")]
        [SerializeField] private GameObject markLocked;

        [Label("通关标记")]
        [SerializeField] private GameObject markDone;

        [Label("点击区域")]
        [SerializeField] private BoxCollider2D hitArea;

        [Header("聚焦取景：相机推进后要框住的范围（窗口局部坐标）")]
        [Label("聚焦框大小")]
        [SerializeField] private Vector2 focusSize = new Vector2(6.8f, 5.0f);

        [Label("聚焦框中心偏移（负数是往下，把窗口下方那排标记也框进来）")]
        [SerializeField] private Vector2 focusOffset = new Vector2(0f, -0.35f);

        [Header("行为")]
        [Label("运行时按视觉自动贴合点击区域")]
        [SerializeField] private bool autoFitHitArea = true;

        private Color[] m_BaseTintColors;
        private EraVisualState m_CurrentState = EraVisualState.Available;

        public EraId Era => era;
        public Color Theme => theme;
        public BoxCollider2D HitArea => hitArea;

        /// <summary>运行时的窗口序号，由 EraWorldController 排序后写入。</summary>
        public int Index { get; private set; } = -1;

        public string Title => string.IsNullOrEmpty(title) ? era.ToString() : title;
        public string Timeline => timeline;

        public void SetIndex(int index) => Index = index;

        /// <summary>
        /// 相机推进到这个矩形里就把整个窗口框住了。
        /// 用序列化值算，不依赖渲染器 —— 窗口被隐藏了、或者还在等渲染器初始化，都一样准。
        /// </summary>
        public Bounds FocusBounds
        {
            get
            {
                Vector3 center = transform.TransformPoint(new Vector3(focusOffset.x, focusOffset.y, 0f));
                Vector3 size = transform.TransformVector(new Vector3(Mathf.Abs(focusSize.x), Mathf.Abs(focusSize.y), 0f));
                return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.001f));
            }
        }

        private void Awake()
        {
            EraWindowRegistry.Register(this);

            EnsureBaseColors();

            if (autoFitHitArea)
            {
                FitHitArea();
            }
        }

        private void OnDestroy() => EraWindowRegistry.Unregister(this);

        public void SetBackdropVisible(bool visible)
        {
            if (backdrop != null)
            {
                backdrop.enabled = visible;
            }
        }

        public void ApplyVisual(EraVisualState state)
        {
            EnsureBaseColors();
            m_CurrentState = state;

            // 锁住的时代整体压暗
            float dim = state == EraVisualState.Locked ? 0.26f : 1f;
            if (stateTinted != null)
            {
                for (int i = 0; i < stateTinted.Length && i < m_BaseTintColors.Length; i++)
                {
                    SpriteRenderer renderer = stateTinted[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.color = Mul(m_BaseTintColors[i], dim);
                }
            }

            if (frameParts != null)
            {
                Color frameColor = Color.Lerp(theme, Color.white, 0.5f);
                switch (state)
                {
                    case EraVisualState.Locked:
                        frameColor = Mul(frameColor, 0.34f);
                        break;
                    case EraVisualState.Completed:
                        frameColor = Color.Lerp(theme, Color.white, 0.82f);
                        break;
                    case EraVisualState.Focused:
                        frameColor = Color.Lerp(theme, Color.white, 0.95f);
                        break;
                }

                for (int i = 0; i < frameParts.Length; i++)
                {
                    if (frameParts[i] != null)
                    {
                        frameParts[i].color = frameColor;
                    }
                }
            }

            if (markLocked != null)
            {
                markLocked.SetActive(state == EraVisualState.Locked);
            }

            if (markDone != null)
            {
                markDone.SetActive(state == EraVisualState.Completed);
            }
        }

        /// <summary>点了一个还没解锁的窗口时抖一下：现在还进不去。</summary>
        public void PlayRejectFeedback()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            EnsureBaseColors();

            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.05f, 0.32f, 8, 0.7f).SetUpdate(true);

            if (frameParts != null)
            {
                for (int i = 0; i < frameParts.Length; i++)
                {
                    if (frameParts[i] != null)
                    {
                        frameParts[i].color = new Color(1f, 0.55f, 0.25f, 1f);
                    }
                }
            }

            if (markLocked != null)
            {
                markLocked.SetActive(true);
            }

            // 抖完把外观还给状态机
            EraVisualState restore = m_CurrentState;
            DOTween.Sequence()
                .AppendInterval(0.55f)
                .AppendCallback(() => ApplyVisual(restore))
                .SetUpdate(true);
        }

        /// <summary>把点击区域贴到内容块 + 边框上。窗口大小改了记得重跑一次。</summary>
        [ContextMenu("按视觉重算点击区域")]
        public void FitHitArea()
        {
            if (hitArea == null)
            {
                return;
            }

            bool has = false;
            Bounds bounds = default;

            if (contentRenderer != null)
            {
                bounds = contentRenderer.bounds;
                has = true;
            }

            if (frameParts != null)
            {
                for (int i = 0; i < frameParts.Length; i++)
                {
                    if (frameParts[i] == null)
                    {
                        continue;
                    }

                    if (has)
                    {
                        bounds.Encapsulate(frameParts[i].bounds);
                    }
                    else
                    {
                        bounds = frameParts[i].bounds;
                        has = true;
                    }
                }
            }

            if (!has)
            {
                return;
            }

            hitArea.offset = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            hitArea.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        private void EnsureBaseColors()
        {
            int count = stateTinted != null ? stateTinted.Length : 0;

            if (m_BaseTintColors != null && m_BaseTintColors.Length == count)
            {
                return;
            }

            m_BaseTintColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                m_BaseTintColors[i] = stateTinted[i] != null ? stateTinted[i].color : Color.white;
            }
        }

        private static Color Mul(Color color, float k)
        {
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }
    }
}
