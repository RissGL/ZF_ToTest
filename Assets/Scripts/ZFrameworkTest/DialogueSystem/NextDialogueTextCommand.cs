using ZGameFramework;

namespace ZF.DialoguePresentation
{
    /// <summary>玩家点「继续」：下一句 / 换组 / 弹选项，全在 Model 里决定</summary>
    public class NextDialogueTextCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetModel<IDialogueShowModel>().MoveNext();
        }
    }
}
