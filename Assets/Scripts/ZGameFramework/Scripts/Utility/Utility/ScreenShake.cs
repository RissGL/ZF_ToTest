using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace ZGameFramework.Utility
{
    using ZGameFramework.Core;

    public class ScreenShake : MonoSingleton<ScreenShake>
    {
        private CinemachineImpulseSource impulseSource;

        protected override void Awake()
        {
            base.Awake();
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        public void Shake(float intensity)
        {
            impulseSource.GenerateImpulse(intensity);
        }
    }
}
