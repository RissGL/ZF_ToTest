using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZGameFramework.Core
{
    public class ResBase
    {
        public string Key { get; private set; }

        public string Path { get; private set; }

        public Object Asset { get; private set; }

        public int RefCount { get; private set; } = 0;

        public bool IsLoaded => Asset != null;
        public bool IsLoading => loadingRequest != null;

        public bool NeedDel=> RefCount <= 0;

        private bool negativeReported=false;

        private ResourceRequest loadingRequest;

        public ResBase(string path, string key)
        {
            Path = path;
            Key = key;
            loadingRequest = null;
        }

        public void Acquire() => RefCount++;

        public void Release()
        {
            if (RefCount <= 0)
            {
                if (!negativeReported)
                {
                    Debug.LogError($"[Res] 引用计数为负！{Key} 的 Release 比 Acquire 多，加载/释放不配对");
                    RefCount = 0;
                }
                return;
            }
            RefCount--;
        }

        public T LoadSync<T>() where T : Object
        {
            if (Asset is T loaded) return loaded;

            if (Asset != null)
            {
                Debug.LogWarning($"[Res] 资源类型不匹配！{Key} 已加载为 " +
                    $"{Asset.GetType().Name}，尝试加载为 {typeof(T).Name}");
            }

            if (loadingRequest != null)
            {
                Debug.LogWarning($"[Res] 资源正在异步加载中，请保持加载方式相同{Key}");
            }

            Asset = Resources.Load<T>(Path);
            loadingRequest = null;

            if (Asset == null)
            {
                Debug.LogWarning($"[Res] 资源加载失败！{Key}");
            }
            return Asset as T;
        }

        public async UniTask<T> LoadAsync<T>(CancellationToken token =default)where T : Object
        {
            if (Asset is T loaded) return loaded;

            if (Asset != null)
            {
                Debug.LogWarning($"[Res] 资源类型不匹配！{Key} 已加载为 " +
                    $"{Asset.GetType().Name}，尝试加载为 {typeof(T).Name}");
            }

            ResourceRequest request = loadingRequest ?? (loadingRequest = Resources.LoadAsync<T>(Path));

            try 
            {
                await request.ToUniTask(cancellationToken: token);
            } 
            finally 
            {
                loadingRequest = null;
            }

            Asset = request.asset;
            if (Asset == null) Debug.LogError($"[Res] 异步加载失败: {Path}");
            return Asset as T;
        }
    }
}