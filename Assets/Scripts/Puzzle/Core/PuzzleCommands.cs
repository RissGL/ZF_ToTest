using ZGameFramework;

namespace ZF.Puzzle
{
    /// <summary>玩家点了场景里的一个物体。动词由 PuzzleSystem 根据「手里有没有道具」自己决定。</summary>
    public class InteractCommand : AbstractCommand
    {
        private readonly string m_TargetId;

        public InteractCommand(string targetId) => m_TargetId = targetId;

        protected override void OnExecute() => this.GetSystem<IPuzzleSystem>().Interact(m_TargetId);
    }

    /// <summary>显式指定动词的交互（留给以后的 UI，比如右键"观察"）。</summary>
    public class ExplicitInteractCommand : AbstractCommand
    {
        private readonly string m_TargetId;
        private readonly Verb m_Verb;
        private readonly string m_ItemId;

        public ExplicitInteractCommand(string targetId, Verb verb, string itemId = "")
        {
            m_TargetId = targetId;
            m_Verb = verb;
            m_ItemId = itemId;
        }

        protected override void OnExecute() => this.GetSystem<IPuzzleSystem>().Interact(m_TargetId, m_Verb, m_ItemId);
    }

    /// <summary>把某个道具拿在手里（itemId 传空 = 松手）。</summary>
    public class SelectItemCommand : AbstractCommand
    {
        private readonly string m_ItemId;

        public SelectItemCommand(string itemId) => m_ItemId = itemId ?? "";

        protected override void OnExecute() => this.GetModel<IPuzzleModel>().SetSelectedItem(m_ItemId);
    }

    /// <summary>在背包里轮换手里拿的东西（背包 UI 做出来之前，用按键凑合）。</summary>
    public class CycleSelectedItemCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            IPuzzleModel model = this.GetModel<IPuzzleModel>();
            if (model.Items.Count == 0)
            {
                model.SetSelectedItem("");
                return;
            }

            string current = model.SelectedItem.Value;

            if (string.IsNullOrEmpty(current))
            {
                model.SetSelectedItem(model.Items[0]);
                return;
            }

            int index = -1;
            for (int i = 0; i < model.Items.Count; i++)
            {
                if (PuzzleOps.SameState(model.Items[i], current))
                {
                    index = i;
                    break;
                }
            }

            int next = index + 1;
            model.SetSelectedItem(next >= model.Items.Count ? "" : model.Items[next]);
        }
    }

    /// <summary>清空所有谜题状态（调试 / 重开一局）。</summary>
    public class ResetPuzzleCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<IPuzzleSystem>().ResetAll();
    }
}
