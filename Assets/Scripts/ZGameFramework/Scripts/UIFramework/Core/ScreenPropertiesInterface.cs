namespace ZGameFramework.UIFramework
{
    public interface IScreenProperties
    {
        
    }
    
    public interface IWindowProperties: IScreenProperties
    {
        WindowPriority WindowQueuePriority { get; set; }
        bool HideOnForegroundLost { get; set; }
        bool IsPopup { get; set; }
        bool SuppressPrefabProperties { get; set; }
    }

    public interface IPanelProperties:IScreenProperties
    {
        PanelPriority Priority { get; set; }
    }
}