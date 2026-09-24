using System;
using UnityEngine;
using ZGameFramework.Core;

namespace ZGameFramework.Utility
{
    public enum AudioChannel
    {
        Master,
        BGM,
        SFX,
        Voice,
    }

    public class AudioVolumeManager : Singleton<AudioVolumeManager>
    {
        private const string KeyMaster = "audio_vol_master";
        private const string KeyBGM = "audio_vol_bgm";
        private const string KeySFX = "audio_vol_sfx";
        private const string KeyVoice = "audio_vol_voice";
        private const string KeyMute = "audio_vol_mute";

        public event Action OnVolumeChanged;

        private readonly float[] m_Volumes = { 1f, 0.8f, 1f, 1f };

        public bool IsMuted { get; private set; }

        private AudioVolumeManager()
        {
            Load();
        }

        public float GetVolume(AudioChannel channel) => m_Volumes[(int)channel];

        public void SetVolume(AudioChannel channel, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(m_Volumes[(int)channel], value))
            {
                return;
            }

            m_Volumes[(int)channel] = value;
            Save();
            OnVolumeChanged?.Invoke();
        }

        public void SetMuted(bool muted)
        {
            if (IsMuted == muted)
            {
                return;
            }

            IsMuted = muted;
            Save();
            OnVolumeChanged?.Invoke();
        }

        public void ToggleMute() => SetMuted(!IsMuted);

        public float GetFinalVolume(AudioChannel channel)
        {
            if (IsMuted)
            {
                return 0f;
            }

            if (channel == AudioChannel.Master)
            {
                return m_Volumes[(int)AudioChannel.Master];
            }

            return m_Volumes[(int)channel] * m_Volumes[(int)AudioChannel.Master];
        }

        private void Load()
        {
            m_Volumes[(int)AudioChannel.Master] = PlayerPrefs.GetFloat(KeyMaster, 1f);
            m_Volumes[(int)AudioChannel.BGM] = PlayerPrefs.GetFloat(KeyBGM, 0.8f);
            m_Volumes[(int)AudioChannel.SFX] = PlayerPrefs.GetFloat(KeySFX, 1f);
            m_Volumes[(int)AudioChannel.Voice] = PlayerPrefs.GetFloat(KeyVoice, 1f);
            IsMuted = PlayerPrefs.GetInt(KeyMute, 0) == 1;
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, m_Volumes[(int)AudioChannel.Master]);
            PlayerPrefs.SetFloat(KeyBGM, m_Volumes[(int)AudioChannel.BGM]);
            PlayerPrefs.SetFloat(KeySFX, m_Volumes[(int)AudioChannel.SFX]);
            PlayerPrefs.SetFloat(KeyVoice, m_Volumes[(int)AudioChannel.Voice]);
            PlayerPrefs.SetInt(KeyMute, IsMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
