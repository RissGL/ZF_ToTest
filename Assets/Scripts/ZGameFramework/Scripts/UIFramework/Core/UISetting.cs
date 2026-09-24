using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZGameFramework.UIFramework
{
    [CreateAssetMenu(fileName = "UISetting",menuName = "ZGameFramework/UI/UISetting")]
    public class UISetting:ScriptableObject
    {
        [Label("UIFrame预制体")] [SerializeField]
        private UIFrame templateUIPrefab=null;

        [Label("UI面板")] [SerializeField]
        private List<GameObject> screenToRegister=null;
        
        [Tooltip("实例化时是否停用")] [SerializeField]
        private bool deactivateOnLoad=true;

        public UIFrame CreateUIInstance(IArchitecture architecture, bool instanceAndRegisterScreens=true)
        {
            var newUI = Instantiate(templateUIPrefab);

            ((ICanSetArchitecture)newUI).SetArchitecture(architecture);
            
            if (instanceAndRegisterScreens)
            {
                foreach (var screen in screenToRegister)
                {
                    var screenInstance = Instantiate(screen);
                    var screenController = screenInstance.GetComponent<IScreenController>();

                    if (screenController!=null)
                    {
                        newUI.RegisterScreen(screen.name,screenController,screenInstance.transform);
                        if (deactivateOnLoad&&screenInstance.activeSelf)
                        {
                            screenInstance.SetActive(false);    
                        }
                    }
                    else
                    {
                        Debug.LogError("UIConfig 界面没有screenController"+screen.name);
                    }
                }
            }

            return newUI;
        }

        private void OnValidate()
        {
            List<GameObject> objToRemove = new List<GameObject>();

            for (int i = 0; i < screenToRegister.Count; i++)
            {
                var screenCtl=screenToRegister[i].GetComponent<IScreenController>();
                if (screenCtl==null)
                {
                    objToRemove.Add(screenToRegister[i]);
                }
            }

            if (objToRemove.Count>0)
            {
                Debug.LogError("请勿将非IScreenController放入需要注册的面板配置中");

                foreach (var obj in objToRemove)
                {
                    Debug.LogError($"{obj.name}不是IScreenController，但是却尝试添加到注册面板中");
                    screenToRegister.Remove(obj);
                }
            }
        }
    }
}