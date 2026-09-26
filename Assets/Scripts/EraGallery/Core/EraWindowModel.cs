using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;

namespace ZF.EraGallery
{
    /// <summary>
    /// 四个时代窗口的纯数据：现在看着哪个、相机在干嘛、哪些时代通关了。
    /// 不持有任何 MonoBehaviour，视图那一侧由 EraWorldController 拿着。
    /// </summary>
    public interface IEraWindowModel : IModel
    {
        /// <summary>当前聚焦的窗口序号，-1 = 全景。只负责写数据，相机由监听者去动。</summary>
        IReadOnlyBindableProperty<int> FocusedIndex { get; }

        /// <summary>取景状态，是「相机该往哪儿动」的唯一信号源。</summary>
        IReadOnlyBindableProperty<EraFocusState> FocusState { get; }

        /// <summary>场上有几个时代窗口（由 EraWorldController 在 Awake 里 Setup）。</summary>
        int WindowCount { get; }

        /// <summary>相机正在推进或拉远，这时候不接受新的进入请求。</summary>
        bool IsBusy { get; }

        bool IsCompleted(int index);
        bool IsUnlocked(int index);

        /// <summary>当前的解锁规则。默认 AllOpen：四个时代一开始就都能进。</summary>
        EraUnlockRule UnlockRule { get; }

        void MarkCompleted(int index);

        /// <summary>换解锁规则。切到 PreviousCompleted 的瞬间会重算一轮锁。</summary>
        void SetUnlockRule(EraUnlockRule rule);

        /// <summary>单独开一个时代，不受规则约束（留给剧情/调试）。</summary>
        void ForceUnlock(int index);

        /// <summary>单独锁一个时代，不受规则约束。</summary>
        void ForceLock(int index);

        void Setup(int windowCount, EraUnlockRule unlockRule = EraUnlockRule.AllOpen,
            int startIndex = EraWindowModel.NoFocus);

        void SetFocusedIndex(int index);
        void SetFocusState(EraFocusState state);
    }

    public class EraWindowModel : AbstractModel, IEraWindowModel
    {
        /// <summary>没有聚焦任何窗口（全景）。</summary>
        public const int NoFocus = -1;

        private readonly BindableProperty<int> m_FocusedIndex = new BindableProperty<int>(NoFocus);
        private readonly BindableProperty<EraFocusState> m_FocusState =
            new BindableProperty<EraFocusState>(EraFocusState.Overview);

        private readonly HashSet<int> m_Completed = new HashSet<int>();
        private readonly HashSet<int> m_ForceUnlocked = new HashSet<int>();
        private readonly HashSet<int> m_ForceLocked = new HashSet<int>();

        public IReadOnlyBindableProperty<int> FocusedIndex => m_FocusedIndex;
        public IReadOnlyBindableProperty<EraFocusState> FocusState => m_FocusState;

        public int WindowCount { get; private set; }

        public EraUnlockRule UnlockRule { get; private set; } = EraUnlockRule.AllOpen;

        public bool IsBusy =>
            m_FocusState.Value == EraFocusState.Entering ||
            m_FocusState.Value == EraFocusState.Leaving;

        public void Setup(int windowCount, EraUnlockRule unlockRule = EraUnlockRule.AllOpen,
            int startIndex = NoFocus)
        {
            WindowCount = Mathf.Max(0, windowCount);
            UnlockRule = unlockRule;
            m_FocusedIndex.SetValueWithoutEvent(startIndex);
        }

        public void SetUnlockRule(EraUnlockRule rule) => UnlockRule = rule;

        public bool IsCompleted(int index) => m_Completed.Contains(index);

        /// <summary>
        /// 默认规则下（AllOpen）四个时代一开始就都能进。
        /// 换成 PreviousCompleted 才变成「第一个永远开着，后面的要等前一个通关」。
        /// 单独 ForceLock / ForceUnlock 的优先级最高。
        /// </summary>
        public bool IsUnlocked(int index)
        {
            if (index < 0 || index >= WindowCount)
            {
                return false;
            }

            if (m_ForceLocked.Contains(index))
            {
                return false;
            }

            if (m_ForceUnlocked.Contains(index))
            {
                return true;
            }

            if (UnlockRule == EraUnlockRule.AllOpen)
            {
                return true;
            }

            return index == 0 || m_Completed.Contains(index - 1);
        }

        public void MarkCompleted(int index)
        {
            if (index < 0 || index >= WindowCount)
            {
                return;
            }

            m_Completed.Add(index);
        }

        public void ForceUnlock(int index)
        {
            m_ForceLocked.Remove(index);
            m_ForceUnlocked.Add(index);
        }

        public void ForceLock(int index)
        {
            m_ForceUnlocked.Remove(index);
            m_ForceLocked.Add(index);
        }

        public void SetFocusedIndex(int index) => m_FocusedIndex.Value = index;

        public void SetFocusState(EraFocusState state) => m_FocusState.Value = state;

        protected override void OnInit()
        {
        }
    }
}
