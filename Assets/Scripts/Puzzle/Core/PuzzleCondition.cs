using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 一条条件。挂在规则 / 谜题 / 物体状态规则上，全部满足才算通过。
    ///
    /// 用 [SerializeReference] 多态：Inspector 里能给列表加不同类型的条件，
    /// 每种类型只带自己需要的字段（比"万能条件类 + 一个 kind 下拉"干净）。
    /// </summary>
    [Serializable]
    public abstract class PuzzleCondition
    {
        public abstract bool IsMet(PuzzleContext context);

        /// <summary>给日志 / 校验工具看的可读描述。</summary>
        public virtual string Describe() => GetType().Name;
    }

    /// <summary>世界状态（flag）比较。bool 就是和 0 / 1 比。</summary>
    [Serializable]
    public class FlagCondition : PuzzleCondition
    {
        [Label("flag 名")]
        public string flag = "";

        public FlagOp op = FlagOp.Equals;

        [Label("比较值")]
        public float value = 1f;

        public override bool IsMet(PuzzleContext context) =>
            context.State != null && PuzzleOps.Compare(context.State.GetFlag(flag), op, value);

        public override string Describe() => $"flag[{flag}] {PuzzleOps.OpText(op)} {value}";
    }

    /// <summary>某个物体现在是不是这个状态（由 SetObjectState 效果写的那个）。</summary>
    [Serializable]
    public class ObjectStateCondition : PuzzleCondition
    {
        [Label("物体 id")]
        public string interactableId = "";

        [Label("状态名")]
        public string state = "";

        public override bool IsMet(PuzzleContext context) =>
            context.State != null && PuzzleOps.SameState(context.State.GetObjectState(interactableId), state);

        public override string Describe() => $"物体[{interactableId}] == {state}";
    }

    /// <summary>背包里有没有这个东西。</summary>
    [Serializable]
    public class HasItemCondition : PuzzleCondition
    {
        [Label("道具 id")]
        public string itemId = "";

        public override bool IsMet(PuzzleContext context) =>
            context.State != null && context.State.HasItem(itemId);

        public override string Describe() => $"持有[{itemId}]";
    }

    /// <summary>玩家手里是不是正选着这个东西。</summary>
    [Serializable]
    public class SelectedItemCondition : PuzzleCondition
    {
        [Label("道具 id")]
        public string itemId = "";

        public override bool IsMet(PuzzleContext context) =>
            context.State != null && PuzzleOps.SameState(context.State.SelectedItem.Value, itemId);

        public override string Describe() => $"选中[{itemId}]";
    }

    /// <summary>某个谜题是不是已经解开了。</summary>
    [Serializable]
    public class PuzzleSolvedCondition : PuzzleCondition
    {
        [Label("谜题 id")]
        public string puzzleId = "";

        public override bool IsMet(PuzzleContext context) =>
            context.State != null && context.State.IsSolved(puzzleId);

        public override string Describe() => $"谜题[{puzzleId}] 已解";
    }

    /// <summary>
    /// 玩家现在是不是正进在某个时代窗口里。
    /// 序号 = (int)EraId，和 EraWorldController 里「按 EraId 排序后写入序号」是同一个约定。
    /// </summary>
    [Serializable]
    public class EraFocusedCondition : PuzzleCondition
    {
        public EraId era = EraId.Stone;

        public override bool IsMet(PuzzleContext context) => context.FocusedEraIndex == (int)era;

        public override string Describe() => $"正进在[{era}]里";
    }

    /// <summary>一组条件全部满足。</summary>
    [Serializable]
    public class AndCondition : PuzzleCondition
    {
        [Label("全部满足")]
        [SerializeReference]
        public List<PuzzleCondition> items = new List<PuzzleCondition>();

        public override bool IsMet(PuzzleContext context)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !items[i].IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }

        public override string Describe() => "全部满足";
    }

    /// <summary>一组条件里满足任意一个。</summary>
    [Serializable]
    public class OrCondition : PuzzleCondition
    {
        [Label("任意一个满足")]
        [SerializeReference]
        public List<PuzzleCondition> items = new List<PuzzleCondition>();

        public override bool IsMet(PuzzleContext context)
        {
            if (items.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].IsMet(context))
                {
                    return true;
                }
            }

            return false;
        }

        public override string Describe() => "任意一个满足";
    }

    /// <summary>取反。</summary>
    [Serializable]
    public class NotCondition : PuzzleCondition
    {
        [SerializeReference]
        public PuzzleCondition item;

        public override bool IsMet(PuzzleContext context) => item == null || !item.IsMet(context);

        public override string Describe() => "非(" + (item != null ? item.Describe() : "空") + ")";
    }
}
