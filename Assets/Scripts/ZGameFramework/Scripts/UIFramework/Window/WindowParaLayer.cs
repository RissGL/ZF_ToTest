using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class WindowParaLayer:MonoBehaviour
    {
        [SerializeField] private GameObject darkenBgObject = null;

        private List<GameObject> containedScreens = new List<GameObject>();

        public void AddScreen(Transform screenRectTrans)
        {
            screenRectTrans.SetParent(transform,false);
            containedScreens.Add(screenRectTrans.gameObject);
        }

        public void RefreshDarken()
        {
            for (int i = 0; i < containedScreens.Count; i++)
            {
                if (containedScreens[i] != null)
                {
                    if (containedScreens[i].activeSelf)
                    {
                        darkenBgObject?.SetActive(true);
                        return;
                    }
                }
            }
            
            darkenBgObject.SetActive(false);
        }

        public void DarkenBg()
        {
            darkenBgObject.SetActive(true);
            darkenBgObject.transform.SetAsLastSibling();
        }
        
        
    }
}