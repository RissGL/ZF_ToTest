using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 音效延迟的调度模式
    /// </summary>
    public enum AudioScheduleMode
    {
        /// <summary>dspTime + PlayScheduled：精确到采样，不受 timeScale 影响（暂停也照播）</summary>
        DspTime,

        /// <summary>UniTask.Delay 后 Play：受 timeScale 影响（暂停时延迟也跟着停）</summary>
        UniTaskDelay,
    }

    /// <summary>
    /// 演出音效事件（SO）：一组音效 + 整组统一延迟 + 每个音效各自延迟。
    /// 用法：演出片段引用本 SO，调 Play()（2D）或 Play(position)（3D 空间音效）；
    /// 需要"等音效播完"时用 PlayAsync()。
    /// </summary>
    [CreateAssetMenu(menuName = "AniComment/ShowAudioEventSO")]
    public class ShowAudioEventSO : AudioEvent
    {
        /// <summary>单个音效条目</summary>
        [Serializable]
        public class AudioItem
        {
            [Label("音频")]
            public AudioClip clip;

            [Label("音量")]
            [Range(0f, 1f)] public float volume = 1f;

            [Label("本音效延迟")]
            [Tooltip("在整组统一延迟之后，本音效再延迟多久开始播放")]
            public float delay = 0f;

            [Label("3D 空间音效")]
            [Tooltip("勾选后用 Play(position) 播放才生效：按位置衰减")]
            public bool spatial3D = false;

            [SerializeField, HideInInspector] private bool m_DefaultsApplied;

            /// <summary>
            /// Unity 给 List 新增元素时不会执行字段初始化器（volume 会是 0），
            /// 由宿主 SO 的 OnValidate 调用它补默认值（只补一次，之后用户改 0 不会被覆盖）
            /// </summary>
            public void ApplyDefaultsIfNeeded()
            {
                if (m_DefaultsApplied)
                {
                    return;
                }

                m_DefaultsApplied = true;
                if (volume <= 0f)
                {
                    volume = 1f;
                }
            }
        }

        [Label("音效列表")]
        [SerializeField] private List<AudioItem> audios = new();

        [Label("整组统一延迟")]
        [Tooltip("整组音效统一延迟多久开始（所有音效都往后推这么久）")]
        [SerializeField] private float groupDelay = 0f;

        [Label("调度模式")]
        [Tooltip("DspTime：精确、不受暂停影响；UniTaskDelay：随 timeScale 暂停")]
        [SerializeField] private AudioScheduleMode scheduleMode = AudioScheduleMode.DspTime;

        /// <summary>整组音效播完所需的总时长（供演出序列编排等待用）</summary>
        public float TotalDuration
        {
            get
            {
                float max = groupDelay;
                if (audios == null) return max;

                for (int i = 0; i < audios.Count; i++)
                {
                    var item = audios[i];
                    if (item == null || item.clip == null) continue;
                    float end = groupDelay + item.delay + item.clip.length;
                    if (end > max) max = end;
                }
                return max;
            }
        }

        // ===================== AudioEvent 接口（即发即忘） =====================

        public override void Play() => PlayInternal(Vector3.zero, false);

        public override void Play(Vector3 position) => PlayInternal(position, true);

        // ===================== 可等待版本（演出序列编排用） =====================

        /// <summary>播放并等待整组播完</summary>
        public UniTask PlayAsync() => PlayAsyncInternal(Vector3.zero, false);

        /// <summary>3D 位置版本的播放并等待</summary>
        public UniTask PlayAsync(Vector3 position) => PlayAsyncInternal(position, true);

        // ===================== 分发 =====================

        /// <summary>Inspector 改动时修补：给新建的列表元素补默认值</summary>
        private void OnValidate()
        {
            if (audios == null)
            {
                return;
            }

            for (int i = 0; i < audios.Count; i++)
            {
                audios[i]?.ApplyDefaultsIfNeeded();
            }
        }

        private void PlayInternal(Vector3 position, bool use3D)
        {
            if (audios == null || audios.Count == 0) return;

            if (scheduleMode == AudioScheduleMode.DspTime)
                PlayByDspTime(position, use3D);
            else
                PlayByUniTaskDelay(position, use3D).Forget();
        }

        private UniTask PlayAsyncInternal(Vector3 position, bool use3D)
        {
            if (audios == null || audios.Count == 0) return UniTask.CompletedTask;

            if (scheduleMode == AudioScheduleMode.DspTime)
            {
                PlayByDspTime(position, use3D);
                float wait = TotalDuration;
                return wait > 0f ? UniTask.Delay(TimeSpan.FromSeconds(wait)) : UniTask.CompletedTask;
            }

            return PlayByUniTaskDelay(position, use3D);
        }

        // ===================== 版本 1：dspTime + PlayScheduled =====================

        /// <summary>
        /// 精确调度版：用音频时钟（dspTime）排期，不受帧率/ timeScale 影响。
        /// 适合：演出音效需要和画面精确对齐、游戏中可能会暂停的情况。
        /// </summary>
        private void PlayByDspTime(Vector3 position, bool use3D)
        {
            // ① 整组统一延迟
            double startTime = AudioSettings.dspTime + groupDelay;

            for (int i = 0; i < audios.Count; i++)
            {
                var item = audios[i];
                if (item == null || item.clip == null) continue;

                var source = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);
                SetupSource(source, item, position, use3D);

                // ② 各自延迟：在统一延迟之后再延 item.delay
                source.PlayScheduled(startTime + item.delay);

                // ③ 播完后归还池：整条链路都不受 timeScale 影响 → 用 Realtime 归还
                //    （否则游戏暂停时归还计时停住，池会被占满）
                AudioSourceManager.Instance.ReturnAudioSourceRealtime(
                    source,
                    groupDelay + item.delay + item.clip.length + 0.1f);
            }
        }

        // ===================== 版本 2：UniTask.Delay =====================

        /// <summary>
        /// 异步延迟版：用 UniTask.Delay 计时（受 timeScale 影响，暂停时延迟也停）。
        /// 适合：演出会和游戏一起暂停、且不需要采样级精度的情况。
        /// </summary>
        private async UniTask PlayByUniTaskDelay(Vector3 position, bool use3D)
        {
            // ① 整组统一延迟
            if (groupDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(groupDelay));

            // ② 各音效并行等待自己的延迟
            var tasks = new List<UniTask>(audios.Count);
            for (int i = 0; i < audios.Count; i++)
            {
                var item = audios[i];
                if (item == null || item.clip == null) continue;
                tasks.Add(PlayOneByUniTaskDelay(item, position, use3D));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        private async UniTask PlayOneByUniTaskDelay(AudioItem item, Vector3 position, bool use3D)
        {
            if (item.delay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(item.delay));

            var source = AudioSourceManager.Instance.GetAudioSource(AudioChannel.SFX);
            SetupSource(source, item, position, use3D);
            source.Play();

            await UniTask.Delay(TimeSpan.FromSeconds(item.clip.length));
            AudioSourceManager.Instance.ReturnAudioSource(source, 0f);
        }

        // ===================== 公共 =====================

        private static void SetupSource(AudioSource source, AudioItem item, Vector3 position, bool use3D)
        {
            source.clip = item.clip;
            source.loop = false;

            // 局部音量交给池换算最终音量（局部 × 通道音量 × 主音量，静音为 0）
            AudioSourceManager.Instance.SetLocalVolume(source, item.volume);

            if (use3D && item.spatial3D)
            {
                source.spatialBlend = 1f;              // 3D 空间音效
                source.transform.position = position;
            }
            else
            {
                source.spatialBlend = 0f;              // 2D（演出音效默认）
            }
        }
    }
}
