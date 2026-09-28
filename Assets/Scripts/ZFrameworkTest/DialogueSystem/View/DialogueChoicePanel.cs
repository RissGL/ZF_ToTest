using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 选项面板：组内台词播完 → Model 弹出 choices → DialogueView 把它交给这里显示。
    ///
    /// 职责边界（和气泡一个思路）：
    ///   - 只管「把几个选项摆出来 + 收集玩家的选择」
    ///   - 确认后只发 Chosen 事件，发命令、收面板都是 DialogueView / Model 的事：
    ///     Chosen → DialogueView 发 ChooseDialogueCommand → Model 清掉 choices
    ///     → CurrentChoices 变 null → DialogueView.OnChoicesChanged(null) → 这里的 Hide()
    ///   - 所以面板自己永远不会「选完还挂着」
    ///
    /// 交互：
    ///   - 鼠标：直接点
    ///   - 键盘：↑↓（WS）移动高亮，回车/空格确认，数字键 1-9 直选
    ///   - 弹出那一帧不吃键盘：用来弹出选项的那次按键，不能顺便把第一项确认掉
    ///
    /// 按钮池：面板里摆一个按钮当母版（就是第一个实例），不够再克隆，多余的隐藏 —— 不销毁。
    /// </summary>
    public class DialogueChoicePanel : MonoBehaviour
    {
        [Header("引用")]
        [Label("按钮母版（面板里摆一个，运行时把它当第一个实例用，不够再克隆）")]
        [SerializeField] private DialogueChoiceButton buttonTemplate;

        [Label("按钮容器（挂了 VerticalLayoutGroup 的那个，可空 = 直接用面板根）")]
        [SerializeField] private RectTransform buttonParent;

        [Label("整体淡入淡出用的 CanvasGroup（挂在本物体上）")]
        [SerializeField] private CanvasGroup group;

        [Header("动画")]
        [Label("弹出效果（和气泡一个套路：MangaAnimEffect 列表）")]
        [SerializeReference]
        [SerializeField] private List<MangaAnimEffect> enterEffects;

        [Label("收起效果")]
        [SerializeReference]
        [SerializeField] private List<MangaAnimEffect> exitEffects;

        [Header("排查")]
        [Label("每次弹出选项打印透明度 / 材质（排查「面板发灰」这类问题）")]
        [SerializeField] private bool logStateOnShow = true;

        [Label("确认某个选项时的小动作（可空）")]
        [SerializeReference]
        [SerializeField] private List<MangaAnimEffect> confirmEffects;

        [Header("音频")]
        [Label("弹出音效（可空）")]
        [SerializeField] private ShowAudioEventSO enterAudio;

        [Label("确认音效（可空）")]
        [SerializeField] private ShowAudioEventSO confirmAudio;

        [Header("收起状态（弹出前先回到这里，保证入场动画一定有效果）")]
        [Label("缩放作用对象（Rect = 改 RectTransform 尺寸，界面推荐；Transform = 缩 localScale）")]
        [SerializeField] private VisualScaleMode hiddenScaleMode;

        [Label("收起时的缩放")]
        [SerializeField] private Vector3 hiddenScale = new Vector3(0.9f, 0.9f, 1f);

        [Label("收起时的透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float hiddenAlpha = 0f;

        /// <summary>逻辑上是不是显示中（收起动画还没播完时已经是 false）</summary>
        public bool IsVisible { get; private set; }

        /// <summary>玩家确认了第 index 个选项（下标和 Model.CurrentChoices 对齐）</summary>
        public event Action<int> Chosen;

        private readonly List<DialogueChoiceButton> m_Buttons = new List<DialogueChoiceButton>();
        private int m_Selected = -1;
        private int m_ShownFrame = -1;      // 弹出那一帧忽略键盘输入
        private Vector3 m_BaseScale = Vector3.one;   // 美术在编辑器里摆的缩放（收起/兜底乘在它上面）
        private Vector2 m_BaseSize;                  // 美术在编辑器里摆的尺寸（Rect 模式用）
        private Tween m_CurrentTween;

        private void Awake()
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (transform is RectTransform rect)
            {
                m_BaseSize = rect.sizeDelta;
            }

            m_BaseScale = transform.localScale;

            // 开局先藏好。注意不要 SetActive(false)：
            // 面板常驻但透明（alpha 0 + 不参与射线 + 不响应键盘），Hide 完也只是回到这个状态
            SetVisibleState(false);
        }

        private void OnDestroy()
        {
            m_CurrentTween?.Kill();
            m_CurrentTween = null;
        }

        // ===================== 对外 =====================

        /// <summary>弹出选项（choices 直接用 Model.CurrentChoices 的那份）</summary>
        public void Show(IReadOnlyList<DialogueChoiceData> choices)
        {
            if (choices == null || choices.Count == 0)
            {
                return;
            }

            SetVisibleState(false);          // 先回收起态：入场动画一定有效果

            EnsureButtons(choices.Count);
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                bool used = i < choices.Count;
                m_Buttons[i].gameObject.SetActive(used);
                if (used)
                {
                    m_Buttons[i].Setup(i, choices[i].text);
                }
            }

            SetSelected(0);

            // ★ 兜底（和 Bubble / SlotVisual 一个套路，之前这里漏了）：
            //   上面刚把面板摆回收起态（alpha = hiddenAlpha、缩放 = hiddenScale），
            //   如果入场效果里**没配淡入/缩放**，那段属性就永远停在收起态 ——
            //   hiddenAlpha 配 0 就是整块看不见，配 0.5 就是"有点透明"。
            EffectSequencePlayer.ScanHandled(transform, enterEffects, null,
                out bool handlesAlpha, out bool handlesScale);

            PlayEffects(enterEffects, null);

            if (!handlesAlpha && group != null)
            {
                group.alpha = 1f;
            }

            if (!handlesScale)
            {
                ApplyScale(Vector3.one);
            }

            if (m_CurrentTween == null)
            {
                SetVisibleState(true);       // 没配入场效果（或没配 CanvasGroup）也要保证看得见，别把面板藏死
            }

            // ★★ 交互 / 射线必须**立刻**打开，不能交给动画：
            //   ① `CanvasGroup.interactable = false` 会让子物体里的 Selectable（按钮）进入 Disabled 态，
            //      而 Disabled 的 Color Tint 用的是 m_DisabledColor（默认 alpha 0.5）——
            //      看起来就像"整块半透明"，但 CanvasGroup.alpha 明明是 1、Image.color 也是 1，极难查。
            //   ② blocksRaycasts 留着 false 的话鼠标点不动（键盘导航不看 raycast，所以会以为没事）。
            //   这两样都不是动画属性，没有任何理由等动画。
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            enterAudio?.Play();

            if (logStateOnShow)
            {
                LogState();
            }

            IsVisible = true;
            m_ShownFrame = Time.frameCount;  // 这一帧的空格/回车是"弹出选项"用的，不是确认
        }

        /// <summary>收起（Model 清掉 choices 时由 DialogueView 调；没在显示就是空操作）</summary>
        public void Hide()
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;

            PlayEffects(exitEffects, () =>
            {
                if (!IsVisible)
                {
                    SetVisibleState(false);
                }
            });
        }

        // ===================== 键盘输入 =====================

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            if (Time.frameCount == m_ShownFrame)
            {
                return;                      // 弹出帧不吃输入
            }

            // 上下（WS 也行）移动高亮
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                Move(-1);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                Move(1);
            }

            // 数字键直选 1-9
            int count = VisibleButtonCount();
            for (int i = 0; i < 9 && i < count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Choose(i);
                    return;
                }
            }

            // 确认
            if (m_Selected >= 0
                && (Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.KeypadEnter)
                    || Input.GetKeyDown(KeyCode.Space)))
            {
                Choose(m_Selected);
            }
        }

        private void Move(int delta)
        {
            int count = VisibleButtonCount();
            if (count == 0)
            {
                return;
            }

            SetSelected((m_Selected + delta + count) % count);
        }

        private void SetSelected(int index)
        {
            m_Selected = index;

            for (int i = 0; i < m_Buttons.Count; i++)
            {
                if (m_Buttons[i] != null && m_Buttons[i].gameObject.activeSelf)
                {
                    m_Buttons[i].SetSelected(i == m_Selected);
                }
            }
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= m_Buttons.Count)
            {
                return;
            }

            if (m_Buttons[index] == null || !m_Buttons[index].gameObject.activeSelf)
            {
                return;
            }

            confirmAudio?.Play();
            PlayEffects(confirmEffects, null);
            Chosen?.Invoke(index);           // 收面板是 DialogueView / Model 那边的事
        }

        private int VisibleButtonCount()
        {
            int count = 0;
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                if (m_Buttons[i] != null && m_Buttons[i].gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        // ===================== 按钮池 =====================

        /// <summary>母版就是第一个实例，不够再克隆；多出来的隐藏不销毁</summary>
        private void EnsureButtons(int count)
        {
            if (m_Buttons.Count == 0)
            {
                if (buttonTemplate == null)
                {
                    Debug.LogError("[对话] 选项面板没配按钮母版，弹不出选项");
                    return;
                }

                Hook(buttonTemplate);
                m_Buttons.Add(buttonTemplate);
            }

            while (m_Buttons.Count < count)
            {
                var clone = Instantiate(buttonTemplate, buttonTemplate.transform.parent);
                clone.name = $"{buttonTemplate.name}_{m_Buttons.Count}";
                Hook(clone);
                m_Buttons.Add(clone);
            }
        }

        private void Hook(DialogueChoiceButton button)
        {
            button.Clicked -= HandleButtonClicked;
            button.Clicked += HandleButtonClicked;
        }

        private void HandleButtonClicked(int index)
        {
            Choose(index);
        }

        // ===================== 内部 =====================

        private void SetVisibleState(bool visible)
        {
            ApplyScale(visible ? Vector3.one : hiddenScale);

            if (group != null)
            {
                group.alpha = visible ? 1f : hiddenAlpha;
                group.blocksRaycasts = visible;
                group.interactable = visible;
            }
        }

        /// <summary>按倍数调大小（倍数 = 1 就是回到美术摆的原样），Transform / Rect 两种走法见 hiddenScaleMode</summary>
        private void ApplyScale(Vector3 factor)
        {
            if (hiddenScaleMode == VisualScaleMode.Rect && transform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(m_BaseSize.x * factor.x, m_BaseSize.y * factor.y);
                return;
            }

            transform.localScale = new Vector3(
                m_BaseScale.x * factor.x, m_BaseScale.y * factor.y, m_BaseScale.z * factor.z);
        }

        /// <summary>
        /// 排查用：把面板此刻的"实际透明度 / 材质"打出来。
        /// 面板发灰只有三种来源，一眼能分辨：
        ///   CanvasGroup.Alpha &lt; 1 → 收起态没收干净（hiddenAlpha 配了非 0，或入场动画没淡回 1）
        ///   Image.color.a &lt; 1      → 美术在 Image 上就配了半透明底色
        ///   材质不是 UI/Default    → 溶解擦除的材质没被还原（会按 _Progress 一直发灰）
        /// </summary>
        private void LogState()
        {
            // 自己 + 往上每一层 CanvasGroup 的 alpha（上层压暗是最隐蔽的一种）
            var groupChain = new List<string>();
            for (var t = transform; t != null; t = t.parent)
            {
                if (t.TryGetComponent<CanvasGroup>(out var cg))
                {
                    groupChain.Add($"{t.name}={cg.alpha:0.##}");
                }
            }

            // ★ 子树里**每个物体自己**的 CanvasGroup（之前漏了这块：
            //   面板根 alpha=1 不代表里面每一层都 1；某个中间层被压暗的话外面看不出来）
            var innerGroups = new List<string>();
            foreach (var cg in GetComponentsInChildren<CanvasGroup>(true))
            {
                if (cg != null && cg.alpha < 0.999f)
                {
                    innerGroups.Add($"{cg.name}={cg.alpha:0.##}");
                }
            }

            // 面板子树里每个在用的图元：名字 / 颜色 alpha / 贴图 / 材质（+ TMP 的字体面色 alpha）
            var graphicInfo = new List<string>();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || !graphic.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string spriteName = graphic is Image image && image.sprite != null ? image.sprite.name : "无";
                string materialName = graphic.material != null ? graphic.material.shader.name : "null";
                string faceInfo = "";

                if (graphic is TMP_Text tmp)
                {
                    var fontMaterial = tmp.fontMaterial;
                    if (fontMaterial != null && fontMaterial.HasProperty("_FaceColor"))
                    {
                        var face = fontMaterial.GetColor("_FaceColor");
                        faceInfo = $", 字面色.a={face.a:0.##}";
                    }
                }

                graphicInfo.Add($"{graphic.name}({graphic.GetType().Name}): 实际≈{EffectiveAlpha(graphic):0.##}" +
                                $"（自身色a={graphic.color.a:0.##}）, 图={spriteName}, 材质={materialName}{faceInfo}");
                if (graphicInfo.Count >= 12)
                {
                    graphicInfo.Add("…（更多略）");
                    break;
                }
            }

            Debug.Log($"[对话选项] 弹出时状态：CanvasGroup 链 [{string.Join(" ← ", groupChain)}]\n" +
                      $"  射线/可交互：{(group != null ? $"blocksRaycasts={group.blocksRaycasts}, interactable={group.interactable}" : "无 CanvasGroup")}\n" +
                      $"  子树里 alpha<1 的 CanvasGroup：{(innerGroups.Count == 0 ? "没有" : string.Join(" | ", innerGroups))}\n" +
                      $"  图元 {graphicInfo.Count} 个（实际 = 自己颜色 a × 沿途所有 CanvasGroup 的 a）：\n    " +
                      string.Join("\n    ", graphicInfo), this);
        }

        /// <summary>这个图元"实际看起来"的透明度 = 自己的颜色 a × 沿途所有 CanvasGroup 的 a</summary>
        private static float EffectiveAlpha(Graphic graphic)
        {
            float alpha = graphic.color.a;

            for (var t = graphic.transform; t != null; t = t.parent)
            {
                if (t.TryGetComponent<CanvasGroup>(out var cg))
                {
                    alpha *= cg.alpha;
                }
            }

            return alpha;
        }

        /// <summary>和 Bubble.PlayEffects 一个套路：效果列表拼成一条序列；没配效果就直接回调</summary>
        private void PlayEffects(List<MangaAnimEffect> effects, Action onDone)
        {
            EffectSequencePlayer.Play(ref m_CurrentTween, transform, effects, onDone);
        }
    }
}
