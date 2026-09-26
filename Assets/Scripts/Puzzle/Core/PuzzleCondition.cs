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
        [Label("物体 id（@self = 被点的那个）")]
        public string interactableId = "";

        [Label("状态名")]
        public string state = "";

        public override bool IsMet(PuzzleContext context) =>
            context.State != null &&
            PuzzleOps.SameState(context.State.GetObjectState(PuzzleRef.ResolveSelf(interactableId, context)), state);

        public override string Describe() =>
            $"物体[{(PuzzleRef.IsSelf(interactableId) ? "被点的那个" : interactableId)}] == {state}";
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

    // ===================== 人物相关 =====================

    /// <summary>
    /// 「这个时代」是哪个时代：EraRef 决定。默认写死某个时代；
    /// 选「被点目标所在的时代」就能一条规则管所有裂隙（不用每个时代写一条）。
    /// </summary>
    [Serializable]
    public class CharacterInEraCondition : PuzzleCondition
    {
        [Label("人物 id")]
        public string characterId = "";

        [Label("算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）哪个时代")]
        public EraId era = EraId.Stone;

        public override bool IsMet(PuzzleContext context) =>
            context.Characters != null &&
            context.Characters.IsKnown(characterId) &&
            context.Characters.GetEra(characterId) == PuzzleEraRef.Resolve(eraRef, context, era);

        public override string Describe() =>
            $"人物[{characterId}] 在 {(eraRef == EraRef.Absolute ? era.ToString() : PuzzleEraRef.Text(eraRef))}";
    }

    /// <summary>某个时代里有几个人。「要两个人凑在同一个时代才能解」就用它。</summary>
    [Serializable]
    public class CharacterCountInEraCondition : PuzzleCondition
    {
        [Label("算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）哪个时代")]
        public EraId era = EraId.Stone;

        public FlagOp op = FlagOp.GreaterOrEqual;

        [Label("人数")]
        public float count = 2f;

        public override bool IsMet(PuzzleContext context) =>
            context.Characters != null &&
            PuzzleOps.Compare(context.Characters.CountInEra(PuzzleEraRef.Resolve(eraRef, context, era)), op, count);

        public override string Describe() =>
            $"{(eraRef == EraRef.Absolute ? era.ToString() : PuzzleEraRef.Text(eraRef))} 里的人数 " +
            $"{PuzzleOps.OpText(op)} {count}";
    }

    /// <summary>
    /// 当前点名的是不是某个人。
    ///
    /// characterId：
    ///   留空   = 「有没有点中任何人」—— 用它做「点了人就走单飞、没点人就走全体」的分支
    ///   @self  = 「点名的就是被点的这个」—— 再点一次同一个人 = 取消点名
    /// </summary>
    [Serializable]
    public class SelectedCharacterCondition : PuzzleCondition
    {
        [Label("人物 id（留空 = 有没有点中任何人；@self = 就是被点的这个）")]
        public string characterId = "";

        public override bool IsMet(PuzzleContext context)
        {
            if (context.Characters == null)
            {
                return false;
            }

            string selected = context.Characters.SelectedCharacter.Value;

            if (string.IsNullOrEmpty(characterId))
            {
                return !string.IsNullOrEmpty(selected);
            }

            return PuzzleOps.SameState(selected, PuzzleRef.ResolveSelf(characterId, context));
        }

        public override string Describe()
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return "点中了某个人";
            }

            return PuzzleRef.IsSelf(characterId) ? "点名的是被点的这个" : $"点名的是[{characterId}]";
        }
    }

    /// <summary>当前点名的那个人在不在「这个时代」—— 「只把点中的这个人送过去」的门槛。</summary>
    [Serializable]
    public class SelectedCharacterInEraCondition : PuzzleCondition
    {
        [Label("算哪个时代")]
        public EraRef eraRef = EraRef.Absolute;

        [Label("（EraRef = 指定时代时用）哪个时代")]
        public EraId era = EraId.Stone;

        public override bool IsMet(PuzzleContext context)
        {
            if (context.Characters == null)
            {
                return false;
            }

            string selected = context.Characters.SelectedCharacter.Value;

            return !string.IsNullOrEmpty(selected) &&
                   context.Characters.IsKnown(selected) &&
                   context.Characters.GetEra(selected) == PuzzleEraRef.Resolve(eraRef, context, era);
        }

        public override string Describe() =>
            $"点名的人在 {(eraRef == EraRef.Absolute ? era.ToString() : PuzzleEraRef.Text(eraRef))}";
    }
}
