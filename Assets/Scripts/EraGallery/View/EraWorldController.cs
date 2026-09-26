using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.Game;

namespace ZF.EraGallery
{
    /// <summary>
    /// 四个时代窗口的总控：
    ///   输入（点击 / ESC / 数字键）→ 命令 → System 改 Model → 这里监听 Model 去动相机和窗口外观。
    /// 相机只认「取景状态」，所以从任何地方改 Model 都能得到正确表现（调试、剧情都行）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EraWorldController : MonoBehaviour, IController
    {
        [Header("引用（Tools/时代窗口/搭建四个时代窗口场景 会自动填）")]
        [Label("主相机")]
        [SerializeField] private Camera mainCamera;

        [Label("相机执行器")]
        [SerializeField] private EraCameraRig cameraRig;

        [Label("时代窗口（按时代先后排，左边这份顺序就是 1 号、2 号…）")]
        [SerializeField] private List<EraWindow> windows = new List<EraWindow>();

        [Label("没填引用时自动在场景里找")]
        [SerializeField] private bool autoCollect = true;

        [Header("时代解锁")]
        [Label("解锁规则：AllOpen = 四个时代一开始就都能进（默认）；PreviousCompleted = 一关一关解（接口留着）")]
        [SerializeField] private EraUnlockRule unlockRule = EraUnlockRule.AllOpen;

        [Header("取景留白")]
        [Label("全景留白（1.2 = 四周留 20% 空）")]
        [SerializeField] private float overviewPadding = 1.2f;

        [Label("进入留白")]
        [SerializeField] private float focusPadding = 1.06f;

        [Header("输入")]
        [Label("鼠标点击：左键进、右键退")]
        [SerializeField] private bool enablePointerInput = true;

        [Label("键盘：ESC 退回、数字键 1-4 直达")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        [Label("调试键 C：把当前时代标记成通关（试通关/迁移流程用）")]
        [SerializeField] private bool enableDebugKeys = true;

        [Header("日志")]
        [Label("打印取景状态变化")]
        [SerializeField] private bool logStateChanges = true;

        private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

        /// <summary>点选用的过滤器：只看 trigger，且不受 Physics2D.queriesHitTriggers 这个全局开关影响。</summary>
        private static readonly ContactFilter2D PickFilter = CreatePickFilter();

        private readonly Collider2D[] m_PickResults = new Collider2D[4];

        private IEraWindowModel m_Model;
        private Bounds m_OverviewBounds;
        private float m_LastAspect = 16f / 9f;

        public IArchitecture GetArchitecture() => GameApp.Interface;

        private void Awake()
        {
            m_Model = this.GetModel<IEraWindowModel>();

            CollectWindows();

            for (int i = 0; i < windows.Count; i++)
            {
                windows[i].SetIndex(i);
            }

            m_Model.Setup(windows.Count, unlockRule);

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (cameraRig == null && mainCamera != null)
            {
                cameraRig = mainCamera.GetComponent<EraCameraRig>();
            }

            m_Model.FocusState.Register(OnFocusStateChanged).UnregisterOnDestroyTrigger(this);
            m_Model.FocusedIndex.Register(OnFocusedIndexChanged).UnregisterOnDestroyTrigger(this);
            this.RegisterEvent<EraFocusRejectedEvent>(OnFocusRejected).UnregisterOnDestroyTrigger(this);
            this.RegisterEvent<EraCompletedEvent>(OnEraCompleted).UnregisterOnDestroyTrigger(this);
        }

        private void Start()
        {
            if (windows.Count == 0)
            {
                Debug.LogError("[时代窗口] 一个窗口都没找到。先跑一下菜单 Tools/时代窗口/搭建四个时代窗口场景。");
                enabled = false;
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogError("[时代窗口] 场景里没有主相机（Tag = MainCamera）。");
                enabled = false;
                return;
            }

            m_OverviewBounds = ComputeOverviewBounds();
            m_LastAspect = mainCamera.aspect;

            if (cameraRig == null)
            {
                Debug.LogWarning("[时代窗口] 没找到 EraCameraRig，相机不会动。");
            }
            else
            {
                cameraRig.SnapTo(m_OverviewBounds, overviewPadding);
            }

            RefreshVisuals();

            Debug.Log($"[时代窗口] 就绪：{windows.Count} 个时代窗口（解锁规则 {unlockRule}）。\n" +
                      "  左键点窗口 = 进入　右键 / ESC = 退回全景　数字键 1-4 = 直达　C = 把当前时代标记为通关");
        }

        private void Update()
        {
            if (m_Model == null)
            {
                return;
            }

            HandleAspectChange();

            if (enablePointerInput)
            {
                HandlePointer();
            }

            HandleKeyboard();
        }

        private void OnDestroy()
        {
            if (cameraRig != null)
            {
                cameraRig.KillTweens();
            }
        }

        // ===================== 输入 =====================

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || mainCamera == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                this.SendCommand<ExitEraFocusCommand>();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            // 相机正在动的时候不接新请求，免得玩家点到一半的窗口
            if (m_Model.IsBusy)
            {
                return;
            }

            Vector2 screenPoint = mouse.position.ReadValue();
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));

            EraWindow window = PickWindow(worldPoint);
            if (window == null)
            {
                return;
            }

            this.SendCommand(new FocusEraCommand(window.Index));
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[Key.Escape].wasPressedThisFrame)
            {
                this.SendCommand<ExitEraFocusCommand>();
            }

            if (enableKeyboardShortcuts)
            {
                for (int i = 0; i < windows.Count && i < NumberKeys.Length; i++)
                {
                    if (keyboard[NumberKeys[i]].wasPressedThisFrame)
                    {
                        this.SendCommand(new FocusEraCommand(i));
                        break;
                    }
                }
            }

            if (enableDebugKeys && keyboard[Key.C].wasPressedThisFrame)
            {
                int index = m_Model.FocusedIndex.Value;
                if (index >= 0)
                {
                    this.SendCommand(new CompleteEraCommand(index));
                }
                else if (logStateChanges)
                {
                    Debug.Log("[时代窗口] 先进入一个时代，再按 C 标记通关。");
                }
            }
        }

        private EraWindow PickWindow(Vector2 worldPoint)
        {
            int count = Physics2D.OverlapPoint(worldPoint, PickFilter, m_PickResults);

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = m_PickResults[i];
                if (collider == null)
                {
                    continue;
                }

                EraWindow window = collider.GetComponentInParent<EraWindow>();
                if (window != null)
                {
                    return window;
                }
            }

            return null;
        }

        // ===================== 模型 → 表现 =====================

        private void OnFocusStateChanged(EraFocusState state)
        {
            if (logStateChanges)
            {
                Debug.Log($"[时代窗口] 取景状态 → {state}（当前窗口序号 {m_Model.FocusedIndex.Value}）");
            }

            switch (state)
            {
                case EraFocusState.Entering:
                    EnterFocusedWindow();
                    break;

                case EraFocusState.Leaving:
                    LeaveToOverview();
                    break;

                default:
                    RefreshVisuals();
                    break;
            }
        }

        private void OnFocusedIndexChanged(int index) => RefreshVisuals();

        private void EnterFocusedWindow()
        {
            EraWindow target = GetWindow(m_Model.FocusedIndex.Value);
            if (target == null)
            {
                // 数据对不上（比如场景里少配了窗口），别把状态卡在「正在进入」
                this.SendCommand<EraTransitionFinishedCommand>();
                return;
            }

            // 进到某个时代里，就只留它一个：既省事，也保证任何屏幕比例下都不会从边上漏出邻居
            for (int i = 0; i < windows.Count; i++)
            {
                EraWindow window = windows[i];
                if (window == null)
                {
                    continue;
                }

                bool isTarget = window == target;
                window.SetBackdropVisible(isTarget);

                if (window.gameObject.activeSelf != isTarget)
                {
                    window.gameObject.SetActive(isTarget);
                }
            }

            RefreshVisuals();
            PlayCamera(target.FocusBounds, focusPadding, true);
        }

        private void LeaveToOverview()
        {
            // 先把四个窗口放出来，再拉远 —— 退出去的过程里它们就在画面里了
            for (int i = 0; i < windows.Count; i++)
            {
                EraWindow window = windows[i];
                if (window == null)
                {
                    continue;
                }

                if (!window.gameObject.activeSelf)
                {
                    window.gameObject.SetActive(true);
                }

                window.SetBackdropVisible(false);
            }

            RefreshVisuals();
            PlayCamera(m_OverviewBounds, overviewPadding, false);
        }

        private void PlayCamera(Bounds bounds, float padding, bool zoomingIn)
        {
            if (cameraRig == null)
            {
                this.SendCommand<EraTransitionFinishedCommand>();
                return;
            }

            cameraRig.PlayTo(bounds, padding, zoomingIn, () => this.SendCommand<EraTransitionFinishedCommand>());
        }

        private void RefreshVisuals()
        {
            if (m_Model == null)
            {
                return;
            }

            int focusedIndex = m_Model.FocusedIndex.Value;
            EraFocusState focusState = m_Model.FocusState.Value;
            bool insideSomeWindow = focusState == EraFocusState.Entering || focusState == EraFocusState.Focused;

            for (int i = 0; i < windows.Count; i++)
            {
                EraWindow window = windows[i];
                if (window == null)
                {
                    continue;
                }

                EraVisualState visual;
                if (insideSomeWindow && i == focusedIndex)
                {
                    visual = EraVisualState.Focused;
                }
                else if (m_Model.IsCompleted(i))
                {
                    visual = EraVisualState.Completed;
                }
                else if (!m_Model.IsUnlocked(i))
                {
                    visual = EraVisualState.Locked;
                }
                else
                {
                    visual = EraVisualState.Available;
                }

                window.ApplyVisual(visual);
            }
        }

        private void OnFocusRejected(EraFocusRejectedEvent e)
        {
            EraWindow window = GetWindow(e.Index);
            if (window != null)
            {
                window.PlayRejectFeedback();
            }

            if (logStateChanges)
            {
                string name = window != null ? window.Title : $"#{e.Index}";
                Debug.Log($"[时代窗口] 「{name}」还不能进：{e.Hint}");
            }
        }

        private void OnEraCompleted(EraCompletedEvent e)
        {
            EraWindow done = GetWindow(e.Index);
            string doneName = done != null ? done.Title : $"#{e.Index}";

            if (e.UnlockedIndex >= 0)
            {
                EraWindow next = GetWindow(e.UnlockedIndex);
                string nextName = next != null ? next.Title : $"#{e.UnlockedIndex}";
                Debug.Log($"[时代窗口] 「{doneName}」通关，新解锁 → 「{nextName}」");
            }
            else
            {
                // 默认规则下四个时代一开始就是全开的，所以通关不会新解锁谁
                Debug.Log($"[时代窗口] 「{doneName}」通关。");
            }

            RefreshVisuals();
        }

        // ===================== 杂项 =====================

        private void HandleAspectChange()
        {
            if (mainCamera == null || cameraRig == null)
            {
                return;
            }

            float aspect = mainCamera.aspect;
            if (Mathf.Abs(aspect - m_LastAspect) < 0.0005f)
            {
                return;
            }

            m_LastAspect = aspect;

            // 画面比例变了（拖窗口、换分辨率）就重新贴合一次，不然取景会偏
            if (m_Model.IsBusy)
            {
                return;
            }

            EraWindow focused = GetWindow(m_Model.FocusedIndex.Value);
            if (focused != null && m_Model.FocusState.Value == EraFocusState.Focused)
            {
                cameraRig.SnapTo(focused.FocusBounds, focusPadding);
            }
            else if (m_Model.FocusState.Value == EraFocusState.Overview)
            {
                m_OverviewBounds = ComputeOverviewBounds();
                cameraRig.SnapTo(m_OverviewBounds, overviewPadding);
            }
        }

        private Bounds ComputeOverviewBounds()
        {
            List<Bounds> parts = new List<Bounds>(windows.Count);

            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] != null)
                {
                    parts.Add(windows[i].FocusBounds);
                }
            }

            return EraCameraRig.Encapsulate(parts);
        }

        private EraWindow GetWindow(int index)
        {
            if (index < 0 || index >= windows.Count)
            {
                return null;
            }

            return windows[index];
        }

        private void CollectWindows()
        {
            if (windows == null)
            {
                windows = new List<EraWindow>();
            }

            windows.RemoveAll(window => window == null);

            if (!autoCollect || windows.Count > 0)
            {
                SortWindows();
                return;
            }

            windows.AddRange(FindObjectsOfType<EraWindow>(true));
            SortWindows();
        }

        private void SortWindows()
        {
            windows.Sort((a, b) => a.Era.CompareTo(b.Era));
        }

        /// <summary>挨个试这些键，谁按下算谁。ContactFilter2D 的默认值就是「不要」。</summary>
        private static ContactFilter2D CreatePickFilter()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;
            return filter;
        }
    }
}
