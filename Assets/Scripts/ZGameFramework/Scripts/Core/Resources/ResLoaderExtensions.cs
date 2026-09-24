using System;
using UnityEngine;
using System.ComponentModel;
using Unity.VisualScripting;

namespace ZGameFramework.Core
{
    public static class ResLoaderExtensions
    {
        public static ResLoader BindTo(this ResLoader resLoader ,UnityEngine.Component host)
        { 
            if (resLoader == null || host == null) return resLoader;

            if (resLoader.IsReleased || resLoader.IsRecycled)
            {
                throw new ObjectDisposedException(nameof(ResLoader));
            }

            var recycler=host.GetComponent<ResLoaderRecycler>();

            if (recycler==null)
            {
                recycler = host.AddComponent<ResLoaderRecycler>();
            }

            if (recycler.AddResLoader(resLoader))
            {
                resLoader.OnRelease+=recycler.RemoveResLoader;
            }

            return resLoader;
        }
    }
}