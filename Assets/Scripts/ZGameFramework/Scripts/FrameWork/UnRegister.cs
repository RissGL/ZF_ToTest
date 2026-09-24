using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZGameFramework
{
    using  System.Collections.Generic;
    public interface IUnregister
    {
        void Unregister();
    }

    public interface IUnregisterList
    {
        List<IUnregister> UnregisterList { get; }
    }

    public struct CustomUnregister:IUnregister
    {
        private Action m_OnUnregister { get; set; }
        public CustomUnregister(Action  onUnregister)
            =>m_OnUnregister = onUnregister;

        public void Unregister()
        {
            m_OnUnregister?.Invoke();
            m_OnUnregister = null;
        }
    }
    
    public static class  IUnregisterListExtension
    {
        public static void AddUnregister(this IUnregisterList self, IUnregister register)
            => self.UnregisterList.Add(register); 
        
        public static void UnregisterAll(this IUnregisterList self)
        {
            foreach (var ur in self.UnregisterList)
            {
                ur.Unregister();
            }
            
            self.UnregisterList.Clear();
        }
    }

    public abstract class UnregisterTrigger:MonoBehaviour
    {
        public readonly HashSet<IUnregister> m_Unregister = new();

        public IUnregister AddUnregister(IUnregister register)
        {
            m_Unregister.Add(register);
            return  register;
        }

        public void RemoveUnregister(IUnregister register)
        {
            m_Unregister.Remove(register);
        }

        public void Unregister()
        {
            foreach (var unRegister in m_Unregister)
            {
                unRegister.Unregister();
            }
            m_Unregister.Clear();
        }
    }

    public class UnregisterOnDestroyTrigger : UnregisterTrigger
    {
        private void OnDestroy()
        {
            Unregister();
        }
    }
    
    public class UnregisterOnDisableTrigger: UnregisterTrigger
    {
        private void OnDisable()
        {
            Unregister();
        }
    }

    public class UnregisterCurrentSceneUnloadedTrigger : UnregisterTrigger
    {
        private static UnregisterCurrentSceneUnloadedTrigger m_Default;

        public static UnregisterCurrentSceneUnloadedTrigger Get
        {
            get
            {
                if (m_Default == null)
                {
                    m_Default = new GameObject("UnregisterWhenCurrentSceneUnloadTrigger")
                        .AddComponent<UnregisterCurrentSceneUnloadedTrigger>();
                }
                return m_Default;
            }
        }


        private void Awake()
        {
            DontDestroyOnLoad(this);
            hideFlags = HideFlags.HideInHierarchy;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene) => Unregister();
    }

    public static class UnregisterExtensions
    {
        public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component))
            {
                return  component;
            }
            component = gameObject.AddComponent<T>();
            return component;
        }

        public static IUnregister UnregisterOnDestroyTrigger(this IUnregister self ,
            GameObject gameObject)
        {
            return  GetOrAddComponent<UnregisterOnDestroyTrigger>(gameObject)
                .AddUnregister(self);
        }

        public static IUnregister UnregisterOnDestroyTrigger<T>(this IUnregister self
        ,T component) where T : Component
        {
            return component.gameObject.GetOrAddComponent<UnregisterOnDestroyTrigger>().AddUnregister(self);
        }

        public static IUnregister UnregisterOnDisableTrigger(this IUnregister self,
            GameObject gameObject)
        {
            return GetOrAddComponent<UnregisterOnDisableTrigger>(gameObject)
                .AddUnregister(self);
        }

        public static IUnregister UnregisterOnDisableTrigger<T>(this IUnregister self
            , T component) where T : Component
        {
            return component.GetOrAddComponent<UnregisterOnDisableTrigger>()
                .AddUnregister(self);
        }

        public static IUnregister UnregisterWhenCurrentSceneUnloaded(this IUnregister self)
        {
            return UnregisterCurrentSceneUnloadedTrigger.Get.AddUnregister(self);
        }
    }   
}