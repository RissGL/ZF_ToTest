using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class ToastPanelController:UIDemoPanelController
    {
        [SerializeField] private RectTransform toastRect = null;
        [SerializeField] private float toastDuration = 0.5f;
        [SerializeField] private float toastPause = 2f;
        [SerializeField] private Ease toastEase = Ease.Linear;

        private bool isToasting;    
        
        protected override void AddListener()
        {
            this.RegisterEvent<PlayerDataUpdateEvent>(OnDataUpdated).UnregisterOnDestroyTrigger(this);
        }
        
        private void OnDataUpdated(PlayerDataUpdateEvent data) 
        {
            if (isToasting) 
            {
                return;
            }
            
            StartCoroutine(YieldForDOTween());
        }
        
        
        private IEnumerator YieldForDOTween() {
            yield return null;
            
            isToasting = true;
            Sequence seq = DOTween.Sequence();
            seq.Append(toastRect.DOAnchorPosY(0f, toastDuration).SetEase(toastEase));
            seq.AppendInterval(toastPause);
            seq.Append(toastRect.DOAnchorPosY(toastRect.rect.height, toastDuration).SetEase(toastEase));
            seq.OnComplete(() => isToasting = false);

            seq.Play();
        }
    }
}