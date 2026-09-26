using ZGameFramework;

namespace ZF.EraGallery
{
    /// <summary>
    /// 时代窗口的规则层：能不能进、进了算不算通关、通关后解锁谁。
    /// 相机怎么动、窗口长什么样，都不归它管（那是控制器和 Model 的事）。
    /// 默认解锁规则是「四个时代一开始全开」，一关一关解那条链留在 Model 里没启用。
    /// </summary>
    public interface IEraWindowSystem : ISystem
    {
        /// <summary>请求进入某个窗口。被规则挡下来会发 EraFocusRejectedEvent，返回 false。</summary>
        bool TryFocus(int index);

        /// <summary>请求退回全景。</summary>
        bool TryExitFocus();

        /// <summary>相机补间播完了。进/出两个方向都靠这个落到终态。</summary>
        void NotifyTransitionFinished();

        /// <summary>把一个时代标记成通关。EraCompletedEvent 里会带上「因此新解锁的下一个时代」（没有就是 -1）。</summary>
        bool TryComplete(int index);
    }

    public class EraWindowSystem : AbstractSystem, IEraWindowSystem
    {
        protected override void OnInit()
        {
        }

        public bool TryFocus(int index)
        {
            IEraWindowModel model = this.GetModel<IEraWindowModel>();
            if (model == null)
            {
                return false;
            }

            // 相机还在动的时候不接受新请求，免得两个补间打架
            if (model.IsBusy)
            {
                return false;
            }

            if (index < 0 || index >= model.WindowCount)
            {
                return false;
            }

            if (model.FocusState.Value == EraFocusState.Focused && model.FocusedIndex.Value == index)
            {
                return false;
            }

            if (!model.IsUnlocked(index))
            {
                this.SendEvent(new EraFocusRejectedEvent
                {
                    Index = index,
                    Hint = "这个时代还锁着",
                });
                return false;
            }

            // 先写序号再写状态：状态的监听者（相机）要靠序号知道飞到哪个窗口
            model.SetFocusedIndex(index);
            model.SetFocusState(EraFocusState.Entering);

            this.SendEvent(new EraFocusChangedEvent
            {
                Index = index,
                State = EraFocusState.Entering,
            });
            return true;
        }

        public bool TryExitFocus()
        {
            IEraWindowModel model = this.GetModel<IEraWindowModel>();
            if (model == null)
            {
                return false;
            }

            if (model.IsBusy)
            {
                return false;
            }

            if (model.FocusedIndex.Value < 0)
            {
                return false;
            }

            model.SetFocusState(EraFocusState.Leaving);

            this.SendEvent(new EraFocusChangedEvent
            {
                Index = model.FocusedIndex.Value,
                State = EraFocusState.Leaving,
            });
            return true;
        }

        public void NotifyTransitionFinished()
        {
            IEraWindowModel model = this.GetModel<IEraWindowModel>();
            if (model == null)
            {
                return;
            }

            switch (model.FocusState.Value)
            {
                case EraFocusState.Entering:
                    model.SetFocusState(EraFocusState.Focused);
                    this.SendEvent(new EraFocusChangedEvent
                    {
                        Index = model.FocusedIndex.Value,
                        State = EraFocusState.Focused,
                    });
                    break;

                case EraFocusState.Leaving:
                    // 先清序号（窗口高亮跟着灭），再落到全景
                    model.SetFocusedIndex(EraWindowModel.NoFocus);
                    model.SetFocusState(EraFocusState.Overview);
                    this.SendEvent(new EraFocusChangedEvent
                    {
                        Index = EraWindowModel.NoFocus,
                        State = EraFocusState.Overview,
                    });
                    break;
            }
        }

        public bool TryComplete(int index)
        {
            IEraWindowModel model = this.GetModel<IEraWindowModel>();
            if (model == null)
            {
                return false;
            }

            if (index < 0 || index >= model.WindowCount)
            {
                return false;
            }

            if (model.IsCompleted(index))
            {
                return false;
            }

            int next = index + 1 < model.WindowCount ? index + 1 : -1;

            // 记「因为这次通关而**新**解锁的下一个时代」：
            // 默认规则（四个时代一开始全开）下这里永远是 -1，只有切到 PreviousCompleted 才有值。
            bool nextWasUnlocked = next >= 0 && model.IsUnlocked(next);
            model.MarkCompleted(index);
            bool nextNowUnlocked = next >= 0 && model.IsUnlocked(next);
            int unlocked = next >= 0 && !nextWasUnlocked && nextNowUnlocked ? next : -1;

            this.SendEvent(new EraCompletedEvent
            {
                Index = index,
                UnlockedIndex = unlocked,
            });
            return true;
        }
    }
}
