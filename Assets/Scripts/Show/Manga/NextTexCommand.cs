using UnityEngine;
using ZGameFramework;
using ZGameFramework.Core;

namespace ZF.DialoguePresentation
{
    /*
    public class NextTexEvent : GameEvent
    {
        public int mangaIndex;
        public NextTexEvent(int mangaIndex)
        {
            this.mangaIndex = mangaIndex;
        }
    }*/

    public class TexChangeEnd : GameEvent
    {
        
    }

    public class NextTexCommand:AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetModel<IGravityAniModel>().MoveNext();
            /*this.SendEvent<NextTexEvent>(new NextTexEvent(model.GetNextIndex()));*/
        }
    }
}