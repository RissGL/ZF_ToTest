using System;
using System.Collections;
using UnityEngine;
using ZGameFramework.Core;

namespace ZGameFramework.Core 
{
    public class MonoManager : Singleton<MonoManager>
    {
        private MonoController controller;

        private MonoManager()
        {
            GameObject go = new GameObject("[MonoManager]");
            controller = go.AddComponent<MonoController>();
        }

        public void AddUpdateListener(Action callback)
        {
            controller.AddUpdateListener(callback);
        }

        public void RemoveUpdateListener(Action callback)
        {
            controller.RemoveUpdateListener(callback);
        }

        public void AddFixedUpdateListener(Action callback)
        {
            controller.AddFixedUpdateListener(callback);
        }

        public void RemoveFixedUpdateListener(Action callback)
        {
            controller.RemoveFixedUpdateListener(callback);
        }

        public void AddLateUpdateListener(Action callback)
        {
            controller.AddLateUpdateListener(callback);
        }

        public void RemoveLateUpdateListener(Action callback)
        {
            controller.RemoveLateUpdateListener(callback);
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return controller.StartCoroutine(routine);
        }

        public Coroutine StartCoroutine(string methodName)
        {
            return controller.StartCoroutine(methodName);
        }

        public Coroutine StartCoroutine(string methodName, object value)
        {
            return controller.StartCoroutine(methodName, value);
        }

        public void StopCoroutine(IEnumerator routine)
        {
            controller.StopCoroutine(routine);
        }

        public void StopCoroutine(Coroutine routine)
        {
            controller.StopCoroutine(routine);
        }

        public void StopCoroutine(string methodName)
        {
            controller.StopCoroutine(methodName);
        }

        public void StopAllCoroutines()
        {
            controller.StopAllCoroutines();
        }
    }

}

