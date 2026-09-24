using System;
using UnityEngine;


namespace ZGameFramework.Core 
{
    public class MonoController : MonoBehaviour
    {
        private event Action onUpdate;
        private event Action onFixedUpdate;
        private event Action onLateUpdate;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            onUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            onFixedUpdate?.Invoke();
        }

        private void LateUpdate()
        {
            onLateUpdate?.Invoke();
        }

        private void OnDestroy()
        {
            onUpdate = null;
            onFixedUpdate = null;
            onLateUpdate = null;
        }

        public void AddUpdateListener(Action callback)
        {
            onUpdate += callback;
        }

        public void RemoveUpdateListener(Action callback)
        {
            onUpdate -= callback;
        }

        public void AddFixedUpdateListener(Action callback)
        {
            onFixedUpdate += callback;
        }

        public void RemoveFixedUpdateListener(Action callback)
        {
            onFixedUpdate -= callback;
        }

        public void AddLateUpdateListener(Action callback)
        {
            onLateUpdate += callback;
        }

        public void RemoveLateUpdateListener(Action callback)
        {
            onLateUpdate -= callback;
        }
    }

}
