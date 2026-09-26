using ZGameFramework;

namespace ZF.DialoguePresentation
{
    /// <summary>玩家点了第 index 个选项：跳到他选的那个组</summary>
    public class ChooseDialogueCommand : AbstractCommand
    {
        private readonly int m_Index;

        public ChooseDialogueCommand(int index)
        {
            m_Index = index;
        }

        protected override void OnExecute()
        {
            this.GetModel<IDialogueShowModel>().Choose(m_Index);
        }
    }
}
