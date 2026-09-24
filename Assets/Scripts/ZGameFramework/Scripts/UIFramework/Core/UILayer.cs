using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public abstract class UILayer<TScreen>:MonoBehaviour where  TScreen:IScreenController
    {
        protected Dictionary<string,TScreen>  registerScreens;
        
        public abstract void ShowScreen(TScreen screen);
        
        public abstract void ShowScreen<TProps>(TScreen screen, TProps props) where TProps:IScreenProperties;
        
        public abstract void HideScreen(TScreen screen);

        public void ShowScreenById(string screenId)
        {
            if (registerScreens.TryGetValue(screenId, out TScreen screen))
            {
                ShowScreen(screen);
            }
            else
            {
                Debug.LogError($"{screenId}未注册");
            }
        }

        public void ShowScreenById<TProps>(string screenId,TProps props) where TProps:IScreenProperties
        {
            if (registerScreens.TryGetValue(screenId, out TScreen screen))
            {
                ShowScreen<TProps>(screen,props);
            }
            else
            {
                Debug.LogError($"{screenId}未注册");
            }
        }

        public void HideScreenById(string screenId)
        {
            if (registerScreens.TryGetValue(screenId, out TScreen screen))
            {
                 HideScreen(screen);
            }
            else
            {
                Debug.LogError($"screenId{screenId}未注册，并尝试Hide");
            }
        }

        public virtual void HideAll(bool shouldAnimateWhenHiding=true)
        {
            foreach (var screen in registerScreens)
            {
                screen.Value.Hide(shouldAnimateWhenHiding);
            }   
        }

        public bool IsScreenRegistered(string screenId)
        {
            return registerScreens.ContainsKey(screenId);
        }

        public virtual void Initialized()
        {
            registerScreens = new Dictionary<string, TScreen>();
        }
        
        public virtual void ReparentScreen(IScreenController controller, Transform screenTransform)
        {
            screenTransform.SetParent(transform,false);
        }

        public virtual void RegisterScreen(string screenId, TScreen screen)
        {
            if (!registerScreens.ContainsKey(screenId))
            {
                ProcessRegisterScreen(screenId, screen);
            }
            else
            {
                Debug.LogError($"重复注册id{screenId}");
            }
        }

        protected virtual void ProcessRegisterScreen(string screenId, TScreen screen)
        {
            screen.ScreenId=screenId;
            screen.ScreenDestroyed += OnScreenDestroyed;
            registerScreens.Add(screenId, screen);   
        }

        protected virtual void OnScreenDestroyed(IScreenController screenController)
        {
            if (!string.IsNullOrEmpty(screenController.ScreenId)
                && registerScreens.ContainsKey(screenController.ScreenId))
            {
                UnregisterScreen(screenController.ScreenId,(TScreen)screenController);
            }
        }

        public virtual void UnregisterScreen(string screenControllerScreenId, TScreen screenController)
        {
            if (registerScreens.ContainsKey(screenControllerScreenId))
            {
                ProcessUnregisterScreen(screenControllerScreenId,screenController);
            }
            else
            {
                Debug.LogError($"不存在注册UIController id{screenControllerScreenId}");
            }
        }

        protected virtual void ProcessUnregisterScreen(string screenControllerScreenId, TScreen screenController)
        {
            screenController.ScreenDestroyed-= OnScreenDestroyed;
            registerScreens.Remove(screenControllerScreenId);
        }
    }
}