using System;
using System.Collections;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class AnimationView : AniComponent
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField] private bool playReverse;

        private Action previousCallBackWhenFinished;

        public override void Animate(Transform target, Action callWhenFinished)
        {
            FinishedPrevious();
            var ani = target.GetComponent<Animation>();

            if (ani == null)
            {
                Debug.LogError("[LegacyAnimationScreenTransition] No Animation component in " + target);
                if (callWhenFinished != null)
                {
                    callWhenFinished();
                }

                return;
            }

            ani.clip=clip;
            StartCoroutine(PlayAnimationRoutine(ani, callWhenFinished));
        }

        private IEnumerator PlayAnimationRoutine(Animation targetAni, Action callWhenFinished)
        {
            previousCallBackWhenFinished = callWhenFinished;
            foreach (AnimationState state in targetAni)
            {
                state.time = playReverse ? state.clip.length : 0f;
                state.speed = playReverse ? -1f : 1f;
            }
            
            targetAni.Play(PlayMode.StopAll);
            yield return new WaitForSeconds(targetAni.clip.length);
            FinishedPrevious();    
        }

        private void FinishedPrevious() 
        {
            if (previousCallBackWhenFinished != null) 
            {
                previousCallBackWhenFinished();
                previousCallBackWhenFinished=null;
            }

            StopAllCoroutines();
        }
    }
}
