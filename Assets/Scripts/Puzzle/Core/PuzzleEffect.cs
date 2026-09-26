using System;
using UnityEngine;
using ZGameFramework.Utility;
using ZF.EraGallery;

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
        [Label("物体 id（@self = 被点的那个）")]
        public string interactableId = "";

        [Label("状态名（小写英文，如 lit / open）")]
        public string state = "";

        public override void Execute(PuzzleContext context) =>
            context.State?.SetObjectState(PuzzleRef.ResolveSelf(interactableId, context), state);

        public override string Describe() =>
            $"物体[{(PuzzleRef.IsSelf(interactableId) ? "被点的那个" : interactableId)}] → {state}";
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
        [Label("要说的话（可用 {目标名} / {人物名} / {这里} / {下一个}）", 3)]
        public string message = "";

        public override void Execute(PuzzleContext context) =>
            context.Feedback?.Invoke(PuzzleText.Format(message, context));

        public override string Describe() => $"反馈：{message}";
    }

    /// <summary>只进 Console 的调试输出，不给玩家看。</summary>
    [Serializable]
    public class DebugLogEffect : PuzzleEffect
    {
        [Label("日志内容（占位符同「说一句话」）")]
        public string message = "";

        public override void Execute(PuzzleContext context) =>
            Debug.Log($"[谜题] {PuzzleText.Format(message, context)}");

        public override string Describe() => $"日志：{message}";
    }

    // ===================== 人物相关 =====================

    /// <summary>
    /// 【动画接口】让某个人播一段动画（不碰状态，只发事件）。
    ///
    /// 典型用法是排成一条链：
    ///   PlayCharacterAnimation("leave") → MoveSelectedCharacter(delayBefore=0.6) → PlayCharacterAnimation("arrive")
    /// 效果基类的 `delayBefore` 负责"等动画播完"，搬家照旧由 Move* 效果做。
    /// characterId 留空 = 当前点名的那个。
    /// </summary>
    [Serializable]
    public class PlayCharacterAnimationEffect : PuzzleEffect
    {
        [Label("人物 id（留空 = 点名的那个人；@self = 被点的那个）")]
        public string characterId = "";

        [Label("动画名（自己约定，比如 leave / arrive）")]
        public string clip = "leave";

        public override void Execute(PuzzleContext context) =>
            context.CharacterOps?.PlayAnimation(
                string.IsNullOrEmpty(characterId) ? "" : PuzzleRef.ResolveSelf(characterId, context), clip);

        public override string Describe() =>
            $"让[{(string.IsNullOrEmpty(characterId) ? "点名的人" : characterId)}]播「{clip}」";
    }

    /// <summary>
    /// 把一个人搬到另一个时代。
    /// 「通过一定方式移动」里的方式就是这条效果 —— 把它放进任何一条规则里（点裂隙、解开谜题、用某个道具）。
    /// </summary>
    [Serializable]
    public class MoveCharacterEffect : PuzzleEffect
    {
        [Label("人物 id（@self = 被点的那个）")]
        public string characterId = "";

        [Label("搬到哪：算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）搬到哪个时代")]
        public EraId targetEra = EraId.Steam;

        public override void Execute(PuzzleContext context) =>
            context.CharacterOps?.TryMove(PuzzleRef.ResolveSelf(characterId, context),
                PuzzleEraRef.Resolve(eraRef, context, targetEra));

        public override string Describe() =>
            $"把[{characterId}]搬到 {(eraRef == EraRef.Absolute ? targetEra.ToString() : PuzzleEraRef.Text(eraRef))}";
    }

    /// <summary>
    /// 把一个人**往前（或往回）送一个时代** —— 目标相对他现在所在的时代算，
    /// 所以「点他，他就往前走一个时代」只需要一条规则，不用每个时代写一条。
    /// </summary>
    [Serializable]
    public class MoveCharacterStepEffect : PuzzleEffect
    {
        [Label("人物 id（@self = 被点的那个）")]
        public string characterId = "";

        [Label("往前 / 往回")]
        public EraStep step = EraStep.NextEra;

        public override void Execute(PuzzleContext context)
        {
            string id = PuzzleRef.ResolveSelf(characterId, context);

            if (context.Characters == null || context.CharacterOps == null || string.IsNullOrEmpty(id))
            {
                return;
            }

            EraId current = context.Characters.GetEra(id);
            EraId target = step == EraStep.PreviousEra ? EraCatalog.Previous(current) : EraCatalog.Next(current);

            context.CharacterOps.TryMove(id, target);
        }

        public override string Describe() =>
            $"把[{characterId}]送到{(step == EraStep.PreviousEra ? "上" : "下")}一个时代";
    }

    /// <summary>
    /// 把一个时代里的人**整体**搬到另一个时代 ——
    /// 「一个时代完成后，窗口里的人一起到下一个时代的窗口去」就是这个。
    /// fromEraRef = 被点目标所在的时代 时，「点哪个裂隙就走哪个时代的人」，一条规则管四个裂隙。
    /// </summary>
    [Serializable]
    public class MoveEraCharactersEffect : PuzzleEffect
    {
        [Label("从哪搬：算哪个时代")]
        public EraRef fromEraRef = EraRef.Absolute;

        [Label("（fromEraRef = 指定时代时用）从哪个时代搬")]
        public EraId fromEra = EraId.Stone;

        [Label("搬到哪：算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）搬到哪个时代")]
        public EraId targetEra = EraId.Steam;

        public override void Execute(PuzzleContext context) =>
            context.CharacterOps?.TryMoveEra(
                PuzzleEraRef.Resolve(fromEraRef, context, fromEra),
                PuzzleEraRef.Resolve(eraRef, context, targetEra));

        public override string Describe() =>
            $"把 {(fromEraRef == EraRef.Absolute ? fromEra.ToString() : PuzzleEraRef.Text(fromEraRef))} 里的人" +
            $"整体搬到 {(eraRef == EraRef.Absolute ? targetEra.ToString() : PuzzleEraRef.Text(eraRef))}";
    }

    /// <summary>
    /// 把**当前点名的那个人**搬到某个时代 —— 「只送他一个人过去」。
    /// 没人点名时什么都不做（所以规则上通常配一个 SelectedCharacterInEraCondition 当门槛）。
    /// eraRef 选「它所在时代的下一个」就是"从哪个裂隙进来，就往下一个时代去"。
    /// </summary>
    [Serializable]
    public class MoveSelectedCharacterEffect : PuzzleEffect
    {
        [Label("搬到哪：算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）搬到哪个时代")]
        public EraId targetEra = EraId.Steam;

        public override void Execute(PuzzleContext context)
        {
            string selected = context.Characters != null ? context.Characters.SelectedCharacter.Value : "";

            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            context.CharacterOps?.TryMove(selected, PuzzleEraRef.Resolve(eraRef, context, targetEra));
        }

        public override string Describe() =>
            $"把点名的那个人搬到 {(eraRef == EraRef.Absolute ? targetEra.ToString() : PuzzleEraRef.Text(eraRef))}";
    }

    /// <summary>
    /// 点名 / 取消点名某个人。
    /// characterId：留空 = 取消点名；@self = 点名**被点的那个**人物（这样一条规则管所有人物）。
    /// 点名的那个就是「单独送走」的对象。
    /// </summary>
    [Serializable]
    public class SelectCharacterEffect : PuzzleEffect
    {
        [Label("人物 id（留空 = 取消点名；@self = 被点的那个）")]
        public string characterId = "";

        public override void Execute(PuzzleContext context) =>
            context.CharacterOps?.Select(PuzzleRef.ResolveSelf(characterId, context));

        public override string Describe()
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return "取消点名";
            }

            return PuzzleRef.IsSelf(characterId) ? "点名被点的这个" : $"点名[{characterId}]";
        }
    }
}
