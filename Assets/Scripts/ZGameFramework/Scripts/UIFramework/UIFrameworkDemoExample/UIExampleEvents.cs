using ZGameFramework.Core;

namespace ZGameFramework.UIFramework
{
    public class StartUIDemoEvent:GameEvent
    {
        
    }

    public class NavigateToWindowEvent : GameEvent
    {
        public string screenId;

        public NavigateToWindowEvent(string screenId)
        {
            this.screenId = screenId;
        }
    }

    public class ShowConfirmationPopupEvent:GameEvent
    {
        public ConfirmationPopupProperties  properties;

        public ShowConfirmationPopupEvent(ConfirmationPopupProperties properties)
        {
            this.properties = properties;
        }
    }
}