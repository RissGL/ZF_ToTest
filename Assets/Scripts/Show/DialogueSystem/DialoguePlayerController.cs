using UnityEngine;
using ZGameFramework;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话播放控制器：把 UI 上的「继续 / 选项」按钮转成命令，数据入口是 chapter。
    /// 显示文本、立绘、选项面板这些表现层的东西，自己订阅 IDialogueShowModel 的
    /// CurrentItem / CurrentGroup / CurrentChoices / IsEnd 来更新。
    /// </summary>
    public class DialoguePlayerController : MonoBehaviour, IController
    {
        [Label("章节数据（Play 时从这里开始播）")]
        [SerializeField] private DialogueChapterSO chapter;

        private IDialogueShowModel Model => this.GetModel<IDialogueShowModel>();

        private void Start()
        {
            Model.StartChapter(chapter);
        }

        /// <summary>绑到「继续」按钮 / 点击空白继续</summary>
        public void NextDialogue()
        {
            this.SendCommand<NextDialogueTextCommand>();
        }

        /// <summary>绑到第 index 个选项按钮</summary>
        public void Choose(int index)
        {
            this.SendCommand(new ChooseDialogueCommand(index));
        }

        public IArchitecture GetArchitecture()
        {
            return GravityAniApp.Interface;
        }
    }
}
