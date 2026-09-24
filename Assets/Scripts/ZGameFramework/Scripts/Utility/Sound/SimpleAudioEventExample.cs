using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.Utility
{
    [CreateAssetMenu(menuName = "BaseSoundEffExampleSO")]
    public class SimpleAudioEventExample : AudioEvent
    {
        [Label("音效数组")]
        [SerializeField] private AudioClip[] clips;

        [Label("基础音量")]
        [SerializeField] private float baseVolume = 1f;

        [Label("基础音调")]
        [SerializeField] private float basePitch = 1f;
        public override void Play()
        {
            if (clips.Length == 0) return;
            AudioSource source = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);

            source.spatialBlend = 0f;
            source.clip = clips[Random.Range(0, clips.Length)];
            source.pitch = basePitch + Random.Range(-0.1f, 0.1f);

            AudioSourceManager.Instance.SetLocalVolume(source, baseVolume + Random.Range(-0.1f, 0.1f));

            source.Play();
            AudioSourceManager.Instance.ReturnAudioSource(source, source.clip.length);
        }

        public override void Play(Vector3 position)
        {
            if (clips.Length == 0) return;

            AudioSource source = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);

            source.transform.position = position;

            source.clip = clips[Random.Range(0, clips.Length)];
            source.pitch = basePitch + Random.Range(-0.1f, 0.1f);

            AudioSourceManager.Instance.SetLocalVolume(source, baseVolume + Random.Range(-0.1f, 0.1f));

            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 100f;

            source.Play();
            AudioSourceManager.Instance.ReturnAudioSource(source, source.clip.length);
        }
    }
}