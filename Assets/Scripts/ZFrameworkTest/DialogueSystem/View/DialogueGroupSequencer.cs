using System;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 换格时序：只管**什么时候做什么**，具体做什么由外面传进来的口决定。
    ///
    /// 时间线（收到新的一组起）：
    ///   ① 锁输入 + 收掉旧气泡 / 选项面板
    ///   ② 转场盖住 → 换模板 + 把本组出场角色分到槽上（观众看不见的一刻）
    ///   ③ 转场露出 → 播组音效 → 立绘 / 框 / 背景播入场动画（观众看得见）
    ///   ④ 入场门放行：这时才弹新格的第一句
    ///
    /// ★ 入场门为什么必须存在：Model 是同步连发的（CurrentGroup 和第一句同一帧到），
    ///   不做门的话第一句会从转场底下透出来，打字机也在观众看不到的时候就开始走。
    ///   编排期间来的台词先缓存，等 ③ 走完再弹；缓存里只留最新的一句。
    ///
    /// 和 <see cref="DialogueInputRouter"/> 的分工：
    ///   本类 = 「换格期间不让玩家推进」（时序问题）
    ///   输入路由 = 「玩家按了一下算什么」（输入问题）
    /// </summary>
    public class DialogueGroupSequencer
    {
        /// <summary>每次换组都播转场（同一个模板也擦）</summary>
        public bool TransitionOnEveryGroup { get; set; } = true;

        /// <summary>正在换格（这时锁输入 + 攒台词）</summary>
        public bool IsComposing { get; private set; }

        private readonly Func<DialogueShowGroupData, IDialogueTransition> m_ResolveTransition;
        private readonly Action<DialogueShowGroupData> m_HideOld;
        private readonly Action m_HoldOld;
        private readonly Action m_ReleaseOld;
        private readonly Action<DialogueShowGroupData> m_Compose;
        private readonly Action<DialogueShowGroupData, Action> m_Reveal;
        private readonly Action<DialogueShowTextItem> m_ShowItem;

        /// <summary>入场门里攒着的台词（只留最新一句）</summary>
        private DialogueShowTextItem m_Pending;

        /// <summary>这一次换格用的是哪个擦除（用来打断上一次）</summary>
        private IDialogueTransition m_Active;

        /// <summary>这次换格旧画面要活到擦完（溶解 / 交叉类擦除）</summary>
        private bool m_KeepOldFrame;

        /// <param name="resolveTransition">按组数据（擦除 id + 方向 + 颜色）取擦除实现（返回 null = 直接切，不做动画）</param>
        /// <param name="hideOld">收掉旧气泡 / 选项面板（普通擦除：一开始就收，反正观众看不见）</param>
        /// <param name="holdOld">保留旧画面（溶解类：模板 Host 别隐藏旧实例，压在新的上面）</param>
        /// <param name="releaseOld">放掉旧画面（擦完调：停用旧实例 + 收旧气泡）</param>
        /// <param name="compose">换模板 + 配槽（盖住那一刻做；溶解类在 Play 一开始就做）</param>
        /// <param name="reveal">组音效 + 立绘入场；做完调回调</param>
        /// <param name="showItem">入场门放行：弹这一句</param>
        public DialogueGroupSequencer(
            Func<DialogueShowGroupData, IDialogueTransition> resolveTransition,
            Action<DialogueShowGroupData> hideOld,
            Action holdOld,
            Action releaseOld,
            Action<DialogueShowGroupData> compose,
            Action<DialogueShowGroupData, Action> reveal,
            Action<DialogueShowTextItem> showItem)
        {
            m_ResolveTransition = resolveTransition;
            m_HideOld = hideOld;
            m_HoldOld = holdOld;
            m_ReleaseOld = releaseOld;
            m_Compose = compose;
            m_Reveal = reveal;
            m_ShowItem = showItem;
        }

        /// <summary>开始换一格</summary>
        public void Begin(DialogueShowGroupData group)
        {
            IsComposing = true;
            m_Pending = null;

            // 上一次擦除还没擦完（玩家连点跳组 / 上一组只停了一瞬）：先收掉，最后一次胜出
            StopActive();
            ReleaseOldFrame();

            var transition = m_ResolveTransition?.Invoke(group);
            m_Active = transition;

            bool willPlay = transition != null && TransitionOnEveryGroup;

            // ★ 溶解 / 交叉类擦除要旧画面活到擦完：这时不收气泡，还要保留旧模板实例
            m_KeepOldFrame = willPlay && transition is IDialogueTransitionKeepsOldFrame;

            if (m_KeepOldFrame)
            {
                m_HoldOld?.Invoke();
            }
            else
            {
                m_HideOld?.Invoke(group);
            }

            if (willPlay)
            {
                transition.Play(() => m_Compose?.Invoke(group), () => Reveal(group));
            }
            else
            {
                m_Compose?.Invoke(group);
                Reveal(group);
            }
        }

        /// <summary>
        /// 入场门：编排期间来的台词先攒着。
        /// 返回 true = 已经缓存住（调用方别再往下弹）；false = 没在编排，正常弹。
        /// </summary>
        public bool TryBuffer(DialogueShowTextItem item)
        {
            if (!IsComposing)
            {
                return false;
            }

            m_Pending = item;
            return true;
        }

        /// <summary>作废当前编排（章末 / 强制清场用）：正在擦的也收掉，旧画面也放掉</summary>
        public void Cancel()
        {
            IsComposing = false;
            m_Pending = null;
            StopActive();
            ReleaseOldFrame();
        }

        /// <summary>把正在播的擦除收掉（不触发任何回调）</summary>
        private void StopActive()
        {
            if (m_Active != null && m_Active.IsPlaying)
            {
                m_Active.Stop();
            }

            m_Active = null;
        }

        /// <summary>放掉被保留的旧画面（只在溶解类擦除用过之后才需要）</summary>
        private void ReleaseOldFrame()
        {
            if (!m_KeepOldFrame)
            {
                return;
            }

            m_KeepOldFrame = false;
            m_ReleaseOld?.Invoke();
        }

        private void Reveal(DialogueShowGroupData group)
        {
            if (m_Reveal == null)
            {
                Finish();
                return;
            }

            m_Reveal(group, Finish);
        }

        /// <summary>入场门放行：先放掉旧画面，再弹新格的第一句</summary>
        private void Finish()
        {
            IsComposing = false;
            ReleaseOldFrame();

            var item = m_Pending;
            m_Pending = null;

            if (item != null)
            {
                m_ShowItem?.Invoke(item);
            }
        }
    }
}
