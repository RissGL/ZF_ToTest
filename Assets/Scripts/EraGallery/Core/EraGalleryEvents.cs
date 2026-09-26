using ZGameFramework.Core;

namespace ZF.EraGallery
{
    /// <summary>聚焦/取景状态变了。Index = -1 表示回到全景。</summary>
    public class EraFocusChangedEvent : GameEvent
    {
        public int Index;
        public EraFocusState State;
    }

    /// <summary>想进但进不去（比如前一个时代还没通关）。</summary>
    public class EraFocusRejectedEvent : GameEvent
    {
        public int Index;
        public string Hint;
    }

    /// <summary>
    /// 一个时代通关了。
    /// UnlockedIndex 是因此解锁的下一个时代窗口（没有下一个就是 -1）。
    /// 后面「窗口里的人迁到下一个时代」也挂在这个事件上。
    /// </summary>
    public class EraCompletedEvent : GameEvent
    {
        public int Index;
        public int UnlockedIndex = -1;
    }
}
