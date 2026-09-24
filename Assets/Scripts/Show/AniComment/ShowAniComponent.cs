using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一条动画条目：哪个物体 + 同时播哪些效果
    /// </summary>
    [Serializable]
    public class MangaAnimItem
    {
        public Transform target;

        [Label("整组延迟")]
        public float groupDelay;

        [SerializeReference]
        public List<MangaAnimEffect> mangaAnimEffects = new();

        [Label("音效")]
        public ShowAudioEventSO audioEff;
    }

    ///  <summary>
    ///演出0动画组件：Inspector 配置，调 Play()
    /// </summary>
    public class ShowAniComponent : MonoBehaviour
    {
        [SerializeField] private List<MangaAnimItem> mangaAniItems = new();
        private bool isPlayed=false;

        public void Reset()
        {
            foreach (var aniItem in mangaAniItems)
            {
                foreach (var ani in aniItem.mangaAnimEffects)
                {
                    ani.ResetAni(aniItem.target);
                }
            }
        }

        public void Play()
        {
            if (isPlayed)
            {
                Reset();
            }
            
            foreach (var aniItem in mangaAniItems)
            {
                var seq = DOTween.Sequence().SetDelay(aniItem.groupDelay);   // 整组延迟

                foreach (var effect in aniItem.mangaAnimEffects)
                {
                    // 造动画
                    var tween = effect.CreateTween(aniItem.target);
                    if (tween != null)          
                    {
                        seq.Join(tween);

                    }
                }

                seq.Play();
                aniItem.audioEff?.Play();                
            }

            isPlayed = true;
        }
    }
}
