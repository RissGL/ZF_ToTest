using System;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.Puzzle
{
    /// <summary>
    /// 一条效果。规则命中后按顺序执行；效果里改的都是 IPuzzleModel 里的权威状态。
    ///
    /// 带副作用的动作（发反馈、完成谜题）不在这里直接做，走 PuzzleContext 上挂的回调，
    /// 这样效果类保持纯逻辑，只有 PuzzleSystem 一个地方发事件。
    /// </summary>
    [Serializable]
    public abstract class PuzzleEffect
    {
        [Label("执行前等多久（秒，0 = 立刻；用来配合动画）")]
        public float delayBefore = 0f;

        public abstract void Execute(PuzzleContext context);

        /// <summary>给日志 / 校验工具看的可读描述。</summary>
        public virtual string Describe() => GetType().Name;
    }

    /// <summary>把世界状态设成某个值（开关就写 0 / 1）。跨时代联动靠的就是这个 —— 写一个全局 flag。</summary>
    [Serializable]
    public class SetFlagEffect : PuzzleEffect
    {
        [Label("flag 名")]
        public string flag = "";

        [Label("值")]
        public float value = 1f;

        public override void Execute(PuzzleContext context) => context.State?.SetFlag(flag, value);

        public override string Describe() => $"设置 flag[{flag}] = {value}";
    }

    /// <summary>把一个 flag 加一点（计数器，比如压力、次数）。</summary>
    [Serializable]
    public class AddFlagEffect : PuzzleEffect
    {
        [Label("flag 名")]
        public string flag = "";

        [Label("增量")]
        public float delta = 1f;

        public override void Execute(PuzzleContext context) => context.State?.AddFlag(flag, delta);

        public override string Describe() => $"flag[{flag}] += {delta}";
    }

    /// <summary>换一个物体的状态（物体自身的表现，和 visualRules 配合）。</summary>
    [Serializable]
    public class SetObjectStateEffect : PuzzleEffect
    {
        [Label("物体 id")]
        public string interactableId = "";

        [Label("状态名（小写英文，如 lit / open）")]
        public string state = "";

        public override void Execute(PuzzleContext context) => context.State?.SetObjectState(interactableId, state);

        public override string Describe() => $"物体[{interactableId}] → {state}";
    }

    /// <summary>往背包里放一个东西。</summary>
    [Serializable]
    public class GiveItemEffect : PuzzleEffect
    {
        [Label("道具 id")]
        public string itemId = "";

        public override void Execute(PuzzleContext context) => context.State?.AddItem(itemId);

        public override string Describe() => $"获得[{itemId}]";
    }

    /// <summary>从背包里拿走一个东西。</summary>
    [Serializable]
    public class ConsumeItemEffect : PuzzleEffect
    {
        [Label("道具 id")]
        public string itemId = "";

        public override void Execute(PuzzleContext context) => context.State?.RemoveItem(itemId);

        public override string Describe() => $"消耗[{itemId}]";
    }

    /// <summary>把某个道具拿在手里（捡到之后不用手动选）。</summary>
    [Serializable]
    public class SelectItemEffect : PuzzleEffect
    {
        [Label("道具 id")]
        public string itemId = "";

        public override void Execute(PuzzleContext context) => context.State?.SetSelectedItem(itemId);

        public override string Describe() => $"选中[{itemId}]";
    }

    /// <summary>
    /// 完成一个谜题。会走 PuzzleSystem 的正式流程：跑它的完成效果、发 PuzzleSolvedEvent，
    /// 主线谜题还会把这个时代标记成通关。
    /// </summary>
    [Serializable]
    public class SolvePuzzleEffect : PuzzleEffect
    {
        [Label("谜题 id")]
        public string puzzleId = "";

        public override void Execute(PuzzleContext context) => context.SolvePuzzle?.Invoke(puzzleId);

        public override string Describe() => $"完成谜题[{puzzleId}]";
    }

    /// <summary>给玩家一句话。UI 还没做，现在会走 PuzzleFeedbackEvent + Console。</summary>
    [Serializable]
    public class FeedbackEffect : PuzzleEffect
    {
        [Label("要说的话")]
        [TextArea]
        public string message = "";

        public override void Execute(PuzzleContext context) => context.Feedback?.Invoke(message);

        public override string Describe() => $"反馈：{message}";
    }

    /// <summary>只进 Console 的调试输出，不给玩家看。</summary>
    [Serializable]
    public class DebugLogEffect : PuzzleEffect
    {
        [Label("日志内容")]
        public string message = "";

        public override void Execute(PuzzleContext context) => Debug.Log($"[谜题] {message}");

        public override string Describe() => $"日志：{message}";
    }
}
