using System;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 转场（换格擦除）接口 —— **流程只认这个口，擦除动画怎么写都行**。
    ///
    /// 为什么是接口而不是一个具体动画：擦除方式肯定不止一种
    /// （横向推过 / 条纹刷入 / 墨水扩散 / 溶解 / 分格线拉开…），
    /// 但换格流程只关心两件事 —— **什么时候盖住**、**什么时候露出结束**。
    /// 所以加一种擦除 = 新增一个实现类（挂到场景里、登记到 DialogueTransitionLibrary），
    /// 时序和 DialogueView 一行都不用改。
    ///
    /// 契约（实现时必须守住，不然会卡输入 / 画面残留）：
    ///   onCovered   —— 画面被**完全盖住**那一刻调：换模板 / 配槽在这里做，观众看不到这一下
    ///   onFinished  —— 露出结束那一刻调：组音效 + 新组部件入场 + 放行第一句
    ///   正常播完时两个回调都要调（漏了 onFinished 会永远锁着输入）
    ///   被 <see cref="Stop"/> 打断时两个都**不调**（调用方自己已经在取消流程了）
    ///
    /// 实现放哪：挂在场景里（**不要放进模板 Prefab**）—— 擦除盖住的那一瞬正是换模板的时候，
    /// 放进模板里的擦除层会跟着模板一起被隐藏，画面会闪。
    /// </summary>
    public interface IDialogueTransition
    {
        /// <summary>正在擦（用来判断要不要打断上一个）</summary>
        bool IsPlaying { get; }

        /// <summary>播一次擦除</summary>
        void Play(Action onCovered, Action onFinished);

        /// <summary>立刻收场：丢掉动画、画面恢复干净，**不触发任何回调**（狂点跳组 / 章末用）</summary>
        void Stop();
    }

    /// <summary>
    /// 兜底实现：不擦，直接切（onCovered 和 onFinished 立刻依次调用）。
    /// 没配擦除、或者擦除 id 找不到时用它 —— 保证流程永远能跑下去。
    /// </summary>
    public class InstantDialogueTransition : MonoBehaviour, IDialogueTransition
    {
        public bool IsPlaying => false;

        public void Play(Action onCovered, Action onFinished)
        {
            onCovered?.Invoke();
            onFinished?.Invoke();
        }

        public void Stop()
        {
            // 没有动画要收
        }
    }
}
