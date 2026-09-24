using System;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public abstract class AniComponent:MonoBehaviour
    {
        public abstract void Animate(Transform target, Action callWhenFinished);
    }
}