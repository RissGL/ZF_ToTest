using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.EraGallery
{
    /// <summary>
    /// 正交相机的执行器：只会做一件事 —— 「挪到某个矩形，并缩放到刚好框住它」。
    /// 它不懂什么叫时代，也不知道谁解锁了；逻辑全在 EraWorldController / Model 那边。
    /// </summary>
    [DisallowMultipleComponent]
    public class EraCameraRig : MonoBehaviour
    {
        [Label("相机（不填就用自己身上的）")]
        [SerializeField] private Camera targetCamera;

        [Header("手感")]
        [Label("推进 / 拉远的时间")]
        [SerializeField] private float transitionDuration = 0.62f;

        [Label("缓动曲线")]
        [SerializeField] private Ease transitionEase = Ease.InOutCubic;

        [Label("起步预备比例：先反向动一点点再走，0 = 不要（推荐 0.05~0.08）")]
        [SerializeField] private float anticipation = 0.06f;

        [Label("不受 timeScale 影响")]
        [SerializeField] private bool ignoreTimeScale = false;

        private Sequence m_Sequence;

        public Camera Cam
        {
            get
            {
                if (targetCamera == null)
                {
                    targetCamera = GetComponent<Camera>();
                }

                return targetCamera;
            }
        }

        /// <summary>补间正在播。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>瞬移 + 直接给到目标大小（初始化、以及窗口尺寸变了的时候用）。</summary>
        public void SnapTo(Bounds bounds, float padding)
        {
            Camera cam = Cam;
            if (cam == null)
            {
                return;
            }

            KillTweens();

            cam.orthographic = true;
            cam.orthographicSize = SizeToFit(bounds, cam.aspect, padding);

            Vector3 position = cam.transform.position;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, position.z);
        }

        /// <summary>
        /// 平滑地推到某个矩形里去。
        /// zoomingIn = true 时先稍微拉远一点再推近，false 时先再凑近一点再拉远 —— 这点反向预备很吃手感。
        /// </summary>
        public void PlayTo(Bounds bounds, float padding, bool zoomingIn, Action onFinished)
        {
            Camera cam = Cam;
            if (cam == null)
            {
                onFinished?.Invoke();
                return;
            }

            cam.orthographic = true;
            KillTweens();

            float duration = Mathf.Max(0.01f, transitionDuration);
            float startSize = cam.orthographicSize;
            Vector3 startPosition = cam.transform.position;

            float targetSize = SizeToFit(bounds, cam.aspect, padding);
            Vector3 targetPosition = new Vector3(bounds.center.x, bounds.center.y, startPosition.z);

            m_Sequence = DOTween.Sequence();
            if (ignoreTimeScale)
            {
                m_Sequence.SetUpdate(true);
            }

            if (anticipation > 0.0001f)
            {
                float preSize = startSize * (zoomingIn ? 1f + anticipation : 1f - anticipation);
                Vector3 prePosition = Vector3.Lerp(startPosition, targetPosition, 0.22f);
                prePosition.z = startPosition.z;
                float preDuration = duration * 0.32f;

                m_Sequence.Append(TweenSize(preSize, preDuration, Ease.OutQuad));
                m_Sequence.Join(cam.transform.DOMove(prePosition, preDuration).SetEase(Ease.OutQuad));
            }

            m_Sequence.Append(TweenSize(targetSize, duration, transitionEase));
            m_Sequence.Join(cam.transform.DOMove(targetPosition, duration).SetEase(transitionEase));

            IsPlaying = true;
            m_Sequence.OnComplete(() =>
            {
                IsPlaying = false;
                m_Sequence = null;
                onFinished?.Invoke();
            });
            m_Sequence.OnKill(() => IsPlaying = false);
        }

        public void KillTweens()
        {
            if (m_Sequence != null)
            {
                m_Sequence.Kill(false);
                m_Sequence = null;
            }

            IsPlaying = false;
        }

        private void OnDestroy() => KillTweens();

        private Tweener TweenSize(float endValue, float duration, Ease ease)
        {
            return DOTween
                .To(() => Cam.orthographicSize, value => Cam.orthographicSize = value, endValue, duration)
                .SetEase(ease);
        }

        /// <summary>把一堆矩形包成一个能把它们全装下的大矩形。</summary>
        public static Bounds Encapsulate(IReadOnlyList<Bounds> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return new Bounds(Vector3.zero, new Vector3(16f, 10f, 0.001f));
            }

            Bounds result = parts[0];
            for (int i = 1; i < parts.Count; i++)
            {
                result.Encapsulate(parts[i]);
            }

            return result;
        }

        /// <summary>正交 size：竖着框住需要 halfHeight，横着框住需要 halfWidth/aspect，取大的那个。</summary>
        public static float SizeToFit(Bounds bounds, float aspect, float padding)
        {
            aspect = Mathf.Max(0.2f, aspect);
            float halfHeight = bounds.extents.y * padding;
            float halfWidth = bounds.extents.x * padding / aspect;
            return Mathf.Max(0.05f, Mathf.Max(halfHeight, halfWidth));
        }
    }
}
