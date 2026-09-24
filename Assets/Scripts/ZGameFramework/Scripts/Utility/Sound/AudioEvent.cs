using UnityEngine;

namespace ZGameFramework.Utility
{
    public abstract class AudioEvent : ScriptableObject
    {
        public abstract void Play();

        public abstract void Play(Vector3 position);
    }
}
