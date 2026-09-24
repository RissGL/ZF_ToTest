using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ZGameFramework.Core 
{
    public static class ResManager
    {
        public static ResLoader GetResLoader()
        {
            return ClassPool<ResLoader>.Get();
        }

        public static int ResCount => ResTable.ResCount;

        public static T Load<T>(string path) where T :UnityEngine.Object
        {
            return ResTable.GetOrCreateRes<T>(path).LoadSync<T>();
        }

        public static UniTask<T> LoadAsync<T>(string path,CancellationToken token=default) where T : UnityEngine.Object
        {
            return ResTable.GetOrCreateRes<T>(path).LoadAsync<T>(token);
        }


        public static async UniTask PreloadAsync(IReadOnlyList<string> paths,
            Action<float> onProgress = null, CancellationToken token = default)
        {
            if (paths == null || paths.Count == 0) return;

            int total = paths.Count;
            var state = new LoadProgressState(total, onProgress);
            var tasks = new UniTask<UnityEngine.Object>[total];

            for (int i = 0; i < total; i++)
            {
                token.ThrowIfCancellationRequested();
                tasks[i] = PreloadOneAsync(paths[i], token, state);
            }

            await UniTask.WhenAll(tasks);
        }

        private static async UniTask<UnityEngine.Object> PreloadOneAsync(string path,
            CancellationToken token, LoadProgressState state)
        {
            var request = Resources.LoadAsync<UnityEngine.Object>(path);
            await request.ToUniTask(cancellationToken: token);
            state.Step();
            return request.asset;
        }

        private class LoadProgressState
        {
            private readonly float total;
            private readonly Action<float> onProgress;
            private int done;

            public LoadProgressState(int total, Action<float> onProgress)
            {
                this.total = total;
                this.onProgress = onProgress;
            }

            public void Step()
            {
                done++;
                onProgress?.Invoke(done / total);
            }
        }


        public static GameObject Instantiate(string path, Transform parent = null)
        {
            var prefab = Load<GameObject>(path);
            return prefab == null ? null : PoolManager.Instance.GetGameObject(prefab, parent);
        }

        public static async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null)
        {
            var prefab =await LoadAsync<GameObject>(path);
            return prefab == null ? null : PoolManager.Instance.GetGameObject(prefab, parent);
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            return prefab == null ? null : PoolManager.Instance.GetGameObject(prefab, parent);
        }

        public static void Recycle(GameObject gameObject)
        {
            if (gameObject != null) PoolManager.Instance.PushGameObject(gameObject);
        }


        public static int GetRefCount<T>(string path) where T : UnityEngine.Object
        {
            return ResTable.GetRefCount<T>(path);
        }

        public static async UniTask UnloadUnused()
        {
            ResTable.ClearUnused();
            await Resources.UnloadUnusedAssets().ToUniTask();
        }
    }
}