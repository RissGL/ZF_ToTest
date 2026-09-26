using ZGameFramework;

namespace ZF.EraGallery
{
    /// <summary>进入某个时代窗口（点击、数字键都走这儿）。</summary>
    public class FocusEraCommand : AbstractCommand
    {
        private readonly int m_Index;

        public FocusEraCommand(int index) => m_Index = index;

        protected override void OnExecute() => this.GetSystem<IEraWindowSystem>().TryFocus(m_Index);
    }

    /// <summary>退回全景。</summary>
    public class ExitEraFocusCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<IEraWindowSystem>().TryExitFocus();
    }

    /// <summary>相机推进/拉远播完了，由 EraWorldController 在补间结束时发。</summary>
    public class EraTransitionFinishedCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<IEraWindowSystem>().NotifyTransitionFinished();
    }

    /// <summary>
    /// 一个时代通关了 —— 下一个时代窗口随之解锁。
    /// 现在只有调试键 C 会发它，正式流程里应该是谜题解开时发。
    /// </summary>
    public class CompleteEraCommand : AbstractCommand
    {
        private readonly int m_Index;

        public CompleteEraCommand(int index) => m_Index = index;

        protected override void OnExecute() => this.GetSystem<IEraWindowSystem>().TryComplete(m_Index);
    }
}
