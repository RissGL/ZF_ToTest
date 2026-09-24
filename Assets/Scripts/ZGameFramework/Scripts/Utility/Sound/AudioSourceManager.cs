using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.Utility
{
    using ZGameFramework.Core;
    public class AudioSourceManager :PersistentMonoSingleton<AudioSourceManager>
    {
        [SerializeField] private AudioSource audioSourcePrefab;

        private int audioSourceCount = 15;
        private Stack<AudioSource> audioSources = new Stack<AudioSource>();

        private readonly Dictionary<AudioSource, AudioChannel> m_Channels = new Dictionary<AudioSource, AudioChannel>();
        private readonly Dictionary<AudioSource, float> m_LocalVolumes = new Dictionary<AudioSource, float>();
        private readonly HashSet<AudioSource> m_Active = new HashSet<AudioSource>();

        private AudioSource m_BGMSource;
        public AudioSource BGMSource => m_BGMSource;

        protected override void Awake()
        {
            base.Awake();

            if (audioSourcePrefab==null)
            {
                audioSourcePrefab = ResManager.Load<GameObject>
                    ("AudioSourcePrefab").GetComponent<AudioSource>();
                Debug.Log("加载");
            }
            
            if (Instance == this)
            {
                for (int i = 0; i < audioSourceCount; i++)
                {
                    AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
                    audioSource.gameObject.SetActive(false);
                    audioSources.Push(audioSource);
                }

                m_BGMSource = Instantiate(audioSourcePrefab, transform);
                m_BGMSource.loop = true;
                m_BGMSource.playOnAwake = false;
                m_BGMSource.gameObject.SetActive(true);
                m_Channels[m_BGMSource] = AudioChannel.BGM;
                m_LocalVolumes[m_BGMSource] = 1f;
                m_Active.Add(m_BGMSource);

                AudioVolumeManager.Instance.OnVolumeChanged += ApplyVolumes;
            }
            
        }

        public AudioSource GetAudioSource(AudioChannel channel = AudioChannel.SFX)
        {
            AudioSource audioSource;
            if (audioSources.Count > 0)
            {
                audioSource = audioSources.Pop();
            }
            else 
            {
                audioSource= Instantiate(audioSourcePrefab, transform);
            }
            audioSource.gameObject.SetActive(true);

            m_Channels[audioSource] = channel;
            m_LocalVolumes[audioSource] = 1f;
            m_Active.Add(audioSource);

            return audioSource;
        }
        
        public void ReturnAudioSource(AudioSource audioSource, float delay)
        {
            StartCoroutine(DelayReturnAudioSource(audioSource, delay));
        }

        public void ReturnAudioSourceRealtime(AudioSource audioSource, float delay)
        {
            StartCoroutine(DelayReturnAudioSourceRealtime(audioSource, delay));
        }

        private IEnumerator DelayReturnAudioSource(AudioSource audioSource, float delay)
        {
            yield return new WaitForSeconds(delay);
            DoReturn(audioSource);
        }

        private IEnumerator DelayReturnAudioSourceRealtime(AudioSource audioSource, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            DoReturn(audioSource);
        }

        private void DoReturn(AudioSource audioSource)
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.Stop();
            audioSource.clip = null;
            audioSource.gameObject.SetActive(false);

            m_Active.Remove(audioSource);
            m_Channels.Remove(audioSource);
            m_LocalVolumes.Remove(audioSource);

            audioSources.Push(audioSource);
        }

        public void SetLocalVolume(AudioSource source, float localVolume)
        {
            if (source == null)
            {
                return;
            }

            m_LocalVolumes[source] = Mathf.Clamp01(localVolume);

            AudioChannel channel = m_Channels.TryGetValue(source, out var ch) ? ch : AudioChannel.SFX;
            source.volume = m_LocalVolumes[source] * AudioVolumeManager.Instance.GetFinalVolume(channel);
        }

        private void ApplyVolumes()
        {
            foreach (var source in m_Active)
            {
                if (source == null)
                {
                    continue;
                }

                AudioChannel channel = m_Channels.TryGetValue(source, out var ch) ? ch : AudioChannel.SFX;
                float local = m_LocalVolumes.TryGetValue(source, out var v) ? v : 1f;
                source.volume = local * AudioVolumeManager.Instance.GetFinalVolume(channel);
            }
        }

        public void PlayBGM(AudioClip clip, bool loop = true)
        {
            if (clip == null || m_BGMSource == null)
            {
                return;
            }

            m_BGMSource.Stop();
            m_BGMSource.clip = clip;
            m_BGMSource.loop = loop;
            SetLocalVolume(m_BGMSource, 1f);
            m_BGMSource.Play();
        }

        public void StopBGM()
        {
            if (m_BGMSource == null)
            {
                return;
            }

            m_BGMSource.Stop();
            m_BGMSource.clip = null;
        }
    }
}
