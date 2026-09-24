using System;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    [Serializable]
    public class PanelProperties:IPanelProperties
    {
        public PanelPriority Priority
        {
            get => panelPriority; 
            set=>panelPriority = value;
        }
        
        [SerializeField]
        [UnityEngine.Tooltip("面板根据其优先级进入不同的副层级。可以在“面板层级”中设置副层级")]
        private PanelPriority  panelPriority;
    }
}