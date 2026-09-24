using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace ZGameFramework.Core
{
    public class ResLoader : IPoolable
    {
        private readonly List<ResBase> resList = new List<ResBase>();

        internal event Action<ResLoader> OnRelease;

        private bool released=false;
        public bool IsReleased => released;
        public bool IsRecycled { get; private set; } = false;
        public ResLoader() { }

        private void ThrowIfNotAvailable()
        {
            if (released || IsRecycled)
            {
                throw new InvalidOperationException("ResLoader 已经被释放或回收，不能再使用。");
            }
        }

        public T LoadSync<T>(string path) where T : UnityEngine.Object
        {
            ThrowIfNotAvailable();
            var res=ResTable.GetOrCreateRes<T>(path);
            res.Acquire();
            resList.Add(res);
            return res.LoadSync<T>();
        }

        public async UniTask<T> LoadAsync<T>(string path,CancellationToken token = default) 
            where T : UnityEngine.Object
        {
            ThrowIfNotAvailable();
            var res=ResTable.GetOrCreateRes<T>(path);
            res.Acquire();
            resList.Add(res);
            try
            {
                return await res.LoadAsync<T>(token);
            } 
            catch
            {
                ResTable.ReleaseRes(res);
                resList.Remove(res);
                throw;
            }
        }

        public void ReleaseAll() 
        {
            if (released) return;
            released = true;

            if (OnRelease != null)
            {
                var handler = OnRelease;
                OnRelease = null;
                handler?.Invoke(this);
            }

            foreach (var res in resList)
            {
                ResTable.ReleaseRes(res);
            }

            resList.Clear();
        }

        public void ReleaseAllAndRecycle()
        {
            if (IsRecycled) return;
            IsRecycled = true;

            ReleaseAll();
            ClassPool<ResLoader>.Recycle(this);
        }

        void IPoolable.OnRecycled()
        {
            IsRecycled = true;
            if (!released)
            {
                ReleaseAll();
            }
            released = true;
            OnRelease = null;
            resList.Clear();
        }

        void IPoolable.OnGet()
        {
            IsRecycled = false;
            OnRelease = null;
            released = false;
        }
    }
}
