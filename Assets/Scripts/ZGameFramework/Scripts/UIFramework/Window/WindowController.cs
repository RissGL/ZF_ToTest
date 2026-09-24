namespace ZGameFramework.UIFramework
{
    public abstract class WindowController : WindowController<WindowProperties>
    {
        
    }

    public abstract class WindowController<TProps>:UIScreenController<TProps>,
        IWindowController where TProps:IWindowProperties
    {
        public bool HideOnForegroundLost
        {
            get => Properties.HideOnForegroundLost;
        }

        public bool IsPopup
        {
            get=>Properties.IsPopup; 
        }

        public WindowPriority Priority
        {
            get=>Properties.WindowQueuePriority; 
        }

        public virtual void UI_CloseWindow()
        {
            CloseRequest(this);
        }

        protected override void SetProperties(TProps properties)
        {
            if (properties!=null)
            {
                if (!properties.SuppressPrefabProperties)
                {
                    properties.HideOnForegroundLost = Properties.HideOnForegroundLost;
                    properties.IsPopup = Properties.IsPopup;
                    properties.WindowQueuePriority = Properties.WindowQueuePriority;
                }

                Properties = properties;
            }
        }

        protected override void HierarchyFixOnShow()
        {
            transform.SetAsLastSibling();
        }
    }
}