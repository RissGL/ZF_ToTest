using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 槽里「会演出的角色部件」的基类：在 <see cref="VisualShowBase"/>（收起态 / 进出场 / 兜底）
    /// 之上，多了「说话人高亮」和「谁站在这里」。
    ///
    /// 继承它的东西（都是槽里的独立部件，互相不影响）：
    ///   PortraitFrame      —— 立绘框（画格）
    ///   Portrait           —— 立绘（画）
    ///   PortraitBackground —— 分镜背景（这一格的底色）
    ///
    /// 动画有两个来源（"播动画的口"，代码不知道你内部有什么部件）：
    ///   ① animSet（共享资产）：效果作用于**本物体**
    ///   ② enterGroups / exitGroups（配在 Prefab 里）：每组 = 目标 Transform + 效果列表
    /// 先后都用效果自己的 delay 排。
    /// </summary>
    public abstract class SlotVisual : VisualShowBase
    {
        [Header("动画①：共享动画集（效果作用于本物体）")]
        [Label("入场 / 退场动画集（可空 = 直接显示 / 直接消失）")]
        public PortraitAnimSet animSet;

        [Header("动画②：带目标的动画组（配在 Prefab 里，驱动本物体下面的部件）")]
        [Label("入场（每组 = 目标 + 效果；目标留空 = 本物体）")]
        [SerializeField] private List<SlotAnimGroup> enterGroups = new List<SlotAnimGroup>();

        [Label("退场")]
        [SerializeField] private List<SlotAnimGroup> exitGroups = new List<SlotAnimGroup>();

        [Header("高亮（说话人亮、其他人暗）")]
        [Label("非说话人的透明度")]
        [Range(0f, 1f)]
        public float dimAlpha = 0.55f;

        [Label("非说话人的缩放")]
        public float dimScale = 0.97f;

        [Label("非说话人的染色")]
        public Color dimColor = new Color(0.72f, 0.72f, 0.78f, 1f);

        [Label("说话人的染色")]
        public Color normalColor = Color.white;

        /// <summary>现在是谁的（null = 空）</summary>
        public DialogueSpeaker Speaker { get; private set; }

        /// <summary>入场动画播完</summary>
        public event Action<SlotVisual> Entered;

        /// <summary>退场动画播完</summary>
        public event Action<SlotVisual> Hidden;

        private bool m_Highlighted = true;

        // ===================== 对外 =====================

        /// <summary>换人 / 换表情（speaker 传 null = 清空）</summary>
        public void SetSpeaker(DialogueSpeaker speaker, DialogueCharExpressionEnum expression)
        {
            Speaker = speaker;
            OnSpeakerChanged(speaker, expression);
        }

        /// <summary>播入场动画（换格露出后调，玩家看得见）</summary>
        public void Enter()
        {
            gameObject.SetActive(true);

            PlayEnter(animSet != null ? animSet.enterEffects : null, enterGroups, () =>
            {
                Entered?.Invoke(this);

                // 入场淡入会把"变暗"覆盖掉，所以入场播完再贴一次高亮状态
                ApplyHighlight(m_Highlighted);
            });
        }

        /// <summary>播退场动画</summary>
        public void Hide()
        {
            PlayExit(animSet != null ? animSet.exitEffects : null, exitGroups,
                () => Hidden?.Invoke(this));
        }

        /// <summary>立刻收起、并清掉角色（换格时用：反正被盖住了，不用播退场）</summary>
        public void ResetToEmpty()
        {
            KillCurrent();

            SetSpeaker(null, DialogueCharExpressionEnum.defaultFace);
            ApplyHiddenState();
            Hidden?.Invoke(this);
        }

        /// <summary>说话人高亮（true = 正常，false = 变暗）</summary>
        public void SetHighlight(bool isSpeaker)
        {
            ApplyHighlight(isSpeaker);
        }

        // ===================== 给子类 =====================

        /// <summary>换人 / 换表情时子类自己处理（立绘换图；框、背景只记一下）</summary>
        protected virtual void OnSpeakerChanged(DialogueSpeaker speaker, DialogueCharExpressionEnum expression)
        {
        }

        /// <summary>高亮时给自己的"主视觉"染色（不想被染色就别重写）</summary>
        protected virtual void OnHighlightColor(bool isSpeaker)
        {
        }

        protected override ShowAudioEventSO GetEnterAudio()
        {
            return animSet != null ? animSet.enterAudio : null;
        }

        protected override ShowAudioEventSO GetExitAudio()
        {
            return animSet != null ? animSet.exitAudio : null;
        }

        // ===================== 内部 =====================

        private void ApplyHighlight(bool isSpeaker)
        {
            m_Highlighted = isSpeaker;

            if (!isSpeaker)
            {
                if (group != null)
                {
                    group.alpha = dimAlpha;
                }

                ApplyScale(Vector3.one * dimScale);
            }
            else
            {
                if (group != null && !IsEntering)
                {
                    group.alpha = 1f;
                }

                ApplyScale(Vector3.one);
            }

            OnHighlightColor(isSpeaker);
        }
    }
}
