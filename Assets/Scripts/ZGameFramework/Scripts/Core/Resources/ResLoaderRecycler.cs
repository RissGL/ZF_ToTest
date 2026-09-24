using UnityEngine;
using System.Collections.Generic;

namespace ZGameFramework.Core
{
    public sealed class ResLoaderRecycler:MonoBehaviour
    {
        private readonly HashSet<ResLoader> resLoaders = new ();

        public bool AddResLoader(ResLoader resLoader)
        {
            if (resLoader == null) return false;
            return resLoaders.Add(resLoader);
        }

        public void RemoveResLoader(ResLoader resLoader)
        {
            if (resLoader == null) return;
            resLoaders.Remove(resLoader);
        }

        private void OnDestroy()
        {
            var loadersToRelease = new List<ResLoader>(resLoaders);
            resLoaders.Clear();

            foreach (var resLoader in loadersToRelease)
            {
                resLoader?.ReleaseAllAndRecycle();
            }
        }
    }

}