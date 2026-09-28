using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 效果序列播放器：把「效果列表 +（可选的）带目标的动画组」拼成一条 DOTween 序列播出去。
    ///
    /// ★ 抽出来的原因：这段逻辑原先在 Bubble / SlotVisual / DialogueChoicePanel 里各抄了一份，
    ///   结果同一个 bug（例如"没配淡入导致永远透明"）要在三个地方各修一遍。
    ///
    /// 语义（三者共用）：
    ///   - 先 kill 掉上一次还在播的 tween
    ///   - 所有效果 Join 进同一条序列（先后顺序由效果自己的 delay 决定）
    ///   - 一个效果都没有 → 直接回调（所以"不配动画 = 瞬间显示/消失"）
    /// </summary>
    public static class EffectSequencePlayer
    {
        /// <summary>
        /// 播放一组效果。current 里上一次的 tween 会先被 kill；没效果时直接回调。
        /// </summary>
        /// <param name="current">调用方保存"当前正在播的 tween"的字段（会被改写）</param>
        /// <param name="self">本物体（效果默认作用在它身上）</param>
        /// <param name="effects">共享动画集里的效果列表（可空）</param>
        /// <param name="groups">带目标的动画组（可空；目标为空 = 作用于 self）</param>
        public static void Play(ref Tween current, Transform self,
            List<MangaAnimEffect> effects, List<SlotAnimGroup> groups, Action onDone)
        {
            current?.Kill();
            current = null;

            Sequence sequence = Join(null, effects, self);

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    Transform target = group.target != null ? group.target : self;
                    sequence = Join(sequence, group.effects, target);
                }
            }

            if (sequence == null)
            {
                onDone?.Invoke();
                return;
            }

            current = sequence;
            sequence.OnComplete(() => onDone?.Invoke());
            sequence.Play();
        }

        /// <summary>没有动画组时的简化重载</summary>
        public static void Play(ref Tween current, Transform self,
            List<MangaAnimEffect> effects, Action onDone)
        {
            Play(ref current, self, effects, null, onDone);
        }

        /// <summary>
        /// 看这份动画有没有效果负责「淡入 / 缩放」——用于"没配动画就强制可见"的兜底判断。
        /// 只统计**作用在本物体上**的效果：驱动子物体的效果不算（它们管的是那些部件）。
        /// </summary>
        public static void ScanHandled(Transform self,
            List<MangaAnimEffect> effects, List<SlotAnimGroup> groups,
            out bool handlesAlpha, out bool handlesScale)
        {
            handlesAlpha = false;
            handlesScale = false;

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    ScanEffect(effect, ref handlesAlpha, ref handlesScale);
                }
            }

            if (groups == null)
            {
                return;
            }

            foreach (var group in groups)
            {
                if (group == null || group.effects == null)
                {
                    continue;
                }

                if (group.target != null && group.target != self)
                {
                    continue;
                }

                foreach (var effect in group.effects)
                {
                    ScanEffect(effect, ref handlesAlpha, ref handlesScale);
                }
            }
        }

        private static void ScanEffect(MangaAnimEffect effect, ref bool handlesAlpha, ref bool handlesScale)
        {
            if (effect is CanvasGroupFadeEffect)
            {
                handlesAlpha = true;
            }
            else if (effect is ScaleEffect)
            {
                handlesScale = true;
            }
        }

        private static Sequence Join(Sequence sequence, List<MangaAnimEffect> effects, Transform target)
        {
            if (effects == null || target == null)
            {
                return sequence;
            }

            foreach (var effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }

                Tween tween = effect.CreateTween(target);
                if (tween == null)
                {
                    continue;
                }

                if (sequence == null)
                {
                    sequence = DOTween.Sequence();
                }

                sequence.Join(tween);
            }

            return sequence;
        }
    }
}
