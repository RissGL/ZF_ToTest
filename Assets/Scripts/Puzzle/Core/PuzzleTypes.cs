using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZF.Puzzle
{
    /// <summary>
    /// 玩家对物体做的动作。
    /// 界面上玩家其实只有两种输入：「点」和「选中道具后点」，
    /// 所以动词不用做「观察 / 打开 / 推」那一套 —— 那些靠规则表里的条件区分。
    /// </summary>
    public enum Verb
    {
        /// <summary>通配：这条规则接受任何动作。</summary>
        Any = 0,

        /// <summary>空手点一下。</summary>
        Interact = 1,

        /// <summary>手里选着一个道具，点这个物体。</summary>
        UseItem = 2,
    }

    /// <summary>
    /// 「往前走一个时代 / 往回退一个时代」。
    /// 目标时代是**相对这个人现在所在的时代**算的 —— 所以一条规则就能管他从哪个时代出发。
    /// </summary>
    public enum EraStep
    {
        NextEra = 0,
        PreviousEra = 1,
    }

    /// <summary>flag 比较方式。bool 也走这套：0 = 假，其它 = 真。</summary>
    public enum FlagOp
    {
        Equals = 0,
        NotEquals = 1,
        Greater = 2,
        GreaterOrEqual = 3,
        Less = 4,
        LessOrEqual = 5,
    }

    /// <summary>状态名约定。状态用字符串而不是枚举，加内容的时候不用改代码。</summary>
    public static class PuzzleStates
    {
        /// <summary>默认状态。物体没被 SetObjectState 改过、也没有状态规则命中时用它。</summary>
        public const string Default = "default";
    }

    /// <summary>
    /// 谜题相关东西的层级。窗内那套是 EraWindowSceneBuilder 定的：
    /// 内容 0 / 地景 5 / 时代序号点 7 / 边框 10 / 锁-对勾 20。
    /// </summary>
    public static class PuzzleSortingOrder
    {
        /// <summary>悬停高亮框（在边框下面，免得糊到窗口边上）。</summary>
        public const int Halo = 9;

        /// <summary>
        /// 物件形状的层级起点。
        /// ★ 同一个东西里**重叠的形状必须给递增的层级**：层级相同又重叠时画的顺序是不确定的，
        ///   表现就是"火时有时无"（壁炉本体把火苗盖住了）。
        ///   StateVisualBehaviour 默认按层级顺序自动排，一般不用管。
        /// </summary>
        public const int ObjectBase = 11;

        /// <summary>人物形状的层级起点。比物件高 —— 人才会站在炉子/箱子前面，而不是被盖住。</summary>
        public const int CharacterBase = 21;
    }

    public static class PuzzleOps
    {
        public static bool Compare(float left, FlagOp op, float right)
        {
            switch (op)
            {
                case FlagOp.Equals: return left == right;
                case FlagOp.NotEquals: return left != right;
                case FlagOp.Greater: return left > right;
                case FlagOp.GreaterOrEqual: return left >= right;
                case FlagOp.Less: return left < right;
                case FlagOp.LessOrEqual: return left <= right;
                default: return false;
            }
        }

        public static string OpText(FlagOp op)
        {
            switch (op)
            {
                case FlagOp.Equals: return "==";
                case FlagOp.NotEquals: return "!=";
                case FlagOp.Greater: return ">";
                case FlagOp.GreaterOrEqual: return ">=";
                case FlagOp.Less: return "<";
                case FlagOp.LessOrEqual: return "<=";
                default: return "?";
            }
        }

        /// <summary>状态名比较。统一走 ordinal，免得文化差异把 "Lit" 和 "lit" 当成一个。</summary>
        public static bool SameState(string a, string b) =>
            string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// 一个状态对应一组物体：开哪一组，这个东西就是哪个状态。
    /// 物件（Interactable）和人物（CharacterView）共用这一套。
    /// </summary>
    [Serializable]
    public class StateGroup
    {
        /// <summary>状态名。</summary>
        public string state = PuzzleStates.Default;

        /// <summary>这一组物体。</summary>
        public List<GameObject> objects = new List<GameObject>();
    }

    /// <summary>
    /// 一次交互 / 一次判定的上下文。条件读它，效果写它。
    /// 条件和效果都是纯逻辑（不碰 MonoBehaviour、不碰架构）；
    /// 需要「发反馈」「完成谜题」「搬人」这类动作时，走这里挂的东西 —— 由 PuzzleSystem 提供。
    /// </summary>
    public sealed class PuzzleContext
    {
        /// <summary>被点的物体 / 人物的 id。</summary>
        public string TargetId = "";

        /// <summary>玩家做的动作。</summary>
        public Verb Verb = Verb.Any;

        /// <summary>用出去的道具 id（动作 = UseItem 时有值）。</summary>
        public string UsedItemId = "";

        /// <summary>正在生效的规则（纯条件判定时可能是 null）。</summary>
        public InteractionRule Rule;

        /// <summary>权威状态：flag / 物体状态 / 背包 / 已解谜题。</summary>
        public IPuzzleModel State;

        /// <summary>人物状态（条件读它：人在哪个时代、一个时代里有几个人）。</summary>
        public ICharacterModel Characters;

        /// <summary>人物操作（效果用它搬人）。</summary>
        public ICharacterSystem CharacterOps;

        /// <summary>玩家现在进在第几个时代里（-1 = 还在全景）。</summary>
        public int FocusedEraIndex = -1;

        /// <summary>给玩家的一句反馈（由 PuzzleSystem 接成事件 + 日志）。</summary>
        public Action<string> Feedback;

        /// <summary>走 PuzzleSystem 的正式流程完成一个谜题（会跑完成效果、发事件、必要时判定时代通关）。</summary>
        public Action<string> SolvePuzzle;
    }
}
