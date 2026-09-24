using UnityEngine;

namespace ZGameFramework.UIFramework
{

    
    [System.Serializable]
    public class WindowProperties:IWindowProperties
    {

        public WindowPriority WindowQueuePriority 
        {
            get=>windowPriority;
            set=>windowPriority = value;
        }
        
        public bool HideOnForegroundLost 
        {
            get => hideOnForegroundLost;
            set=>hideOnForegroundLost=value; 
        }

        public bool IsPopup
        {
            get => isPopup;
            set=>isPopup=value;
        }
        
        public bool SuppressPrefabProperties { get; set; }
        
        
        public WindowProperties(bool hideOnForegroundLost, WindowPriority windowPriority, bool suppressPrefabProperties)
        {
            this.hideOnForegroundLost = hideOnForegroundLost;
            this.windowPriority = windowPriority;
            this.SuppressPrefabProperties=suppressPrefabProperties;
        }
        
        public WindowProperties( WindowPriority windowPriority,bool hideOnForegroundLost=false, bool suppressPrefabProperties=false)
        {
            this.hideOnForegroundLost = hideOnForegroundLost;
            this.windowPriority = windowPriority; ;
            this.SuppressPrefabProperties=suppressPrefabProperties;
        }
        
        public WindowProperties(bool suppressPrefabProperties=false)
        {
            WindowQueuePriority=WindowPriority.ForceForeground;
            HideOnForegroundLost=true;
            SuppressPrefabProperties = suppressPrefabProperties;
        }

        public WindowProperties()
        {
            this.hideOnForegroundLost = true;
            this.windowPriority = WindowPriority.ForceForeground;
            this.isPopup = false;
        }

        
        [SerializeField]
        protected bool hideOnForegroundLost=true;

        [SerializeField]
        protected WindowPriority windowPriority = WindowPriority.ForceForeground;
        
        [SerializeField]
        protected bool isPopup = false;
    }
}