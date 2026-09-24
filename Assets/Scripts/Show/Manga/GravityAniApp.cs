namespace ZF.DialoguePresentation
{
    using ZGameFramework;

    public class GravityAniApp:GameArchitecture<GravityAniApp>
    {
        
        protected override void OnInit()
        {
            this.RegisterModel<IGravityAniModel>(new GravityAniModel(0));
            this.RegisterModel<IDialogueShowModel>(new DialogueShowModel());
        }
    }
}
