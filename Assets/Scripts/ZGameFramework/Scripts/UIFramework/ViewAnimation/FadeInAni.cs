using DG.Tweening;
using System;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class FadeInAni:AniComponent
    {
        [SerializeField] private float duration = 1f;
        [SerializeField] private bool fadeOut=false;

        public override void Animate(Transform target, Action callWhenFinished)
        {
            var canvasGroup = target.gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup=target.gameObject.AddComponent<CanvasGroup>();
            }

            float endAlpha = fadeOut?0f:1f;

            canvasGroup.DOFade(endAlpha, duration).OnComplete(() => callWhenFinished?.Invoke()).SetUpdate(true);
        }
    }
}