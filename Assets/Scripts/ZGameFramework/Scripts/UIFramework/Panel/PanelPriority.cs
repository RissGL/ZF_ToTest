using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public enum PanelPriority
    {
        None=0,
        Priority=1,
        Tutorial=2,
        Blocker=3, 
    }

    [System.Serializable]
    public class PanelPriorityLayerListEntry
    {
        [SerializeField]
        [Tooltip("指定下面板的优先级")]
        private PanelPriority priority;
        [SerializeField]
        [Tooltip("此优先级下所有面板的父节点")]
        private Transform targetParent;

        public PanelPriorityLayerListEntry(PanelPriority priority, Transform targetParent)
        {
            this.priority = priority;
            this.targetParent = targetParent;
        }

        public Transform TargetParent
        {
            get
            {
                return targetParent;
            }
            set 
            {
                targetParent = value;
            }
        }

        public PanelPriority Priority
        {

            get
            {
                return priority;
            }
            set
            {
                priority = value;
            }
        }
        
        
    }

    [System.Serializable]
    public class PanelPriorityLayerList
    {
        [SerializeField]
        [Tooltip("根据面板的优先级查找并存储对应的GameObject，渲染优先级由这些GameObject在层级结构中的顺序决定")]
        private List<PanelPriorityLayerListEntry> paraLayers = null;

        private Dictionary<PanelPriority, Transform> lookup;

        public PanelPriorityLayerList(List<PanelPriorityLayerListEntry> paraLayers)
        {
            this.paraLayers = paraLayers;
        }

        public Dictionary<PanelPriority, Transform> ParaLayerLookup
        {
            get
            {
                if (lookup==null||lookup.Count==0)
                {
                    CacheLookup();
                }
                return lookup;
            }
        }

        private void CacheLookup()
        {
            lookup=new Dictionary<PanelPriority, Transform>();
            for (int i = 0; i < paraLayers.Count; i++)
            {
                lookup.Add(paraLayers[i].Priority,paraLayers[i].TargetParent);
            } 
        }
    }
}