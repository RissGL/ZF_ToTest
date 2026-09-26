using System;
using System.Collections.Generic;
using UnityEngine;
using ZF.EraGallery;

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

    /// <summary>
    /// 「这个时代」是哪个时代 —— 让条件和效果能引用**被点的那个目标所在的时代**。
    ///
    /// 有了它，"哪个裂隙、送到哪"就不用按时代写 4 遍：
    /// 一条「任意裂隙：把点名的人送到它的下一个时代」就管四个裂隙。
    /// 默认是 Absolute（写死的某个时代），所以老数据的含义一个字都不变。
    /// </summary>
    public enum EraRef
    {
        /// <summary>写死的那个时代（用 era 字段）。</summary>
        Absolute = 0,

        /// <summary>被点的目标所在的时代。</summary>
        TargetEra = 1,

        /// <summary>被点的目标所在时代的**下一个**（送人往前走）。</summary>
        TargetEraNext = 2,

        /// <summary>被点的目标所在时代的上一个（送人往回走）。</summary>
        TargetEraPrevious = 3,
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
    /// 目标的**类别**：规则可以按类别一次管一批东西（`@rift` 管四个裂隙），
    /// 不用给每个裂隙各写一条。类别名是自由字符串，写在物体自己身上。
    /// </summary>
    public static class PuzzleCategories
    {
        /// <summary>普通物体（没特别标类别的都算这个）。</summary>
        public const string Prop = "prop";

        /// <summary>人物。CharacterView 自动属于这一类（框架级的，不用手填）。</summary>
        public const string Character = "character";
    }

    /// <summary>
    /// 字段值里的**引用写法**。
    ///
    ///   ""        任意（哪个目标都行）—— 见 InteractionRule.targetId
    ///   "@rift"   类别：目标属于这个类别才匹配
    ///   "@self"   被点的那个目标自己（效果里"点名它自己"、条件里"点的是不是它自己"）
    ///
    /// 就靠一个 `@` 前缀，不新增字段 —— 编辑器里 id 旁边那个 ▾ 会把 `@…` 和真实 id 一起列出来，
    /// 校验也会检查"这个类别在场景里存不存在"，所以不用担心打错字。
    /// </summary>
    public static class PuzzleRef
    {
        public const string Prefix = "@";

        /// <summary>被点的那个目标自己。</summary>
        public const string Self = "@self";

        public static bool IsSelf(string value) =>
            string.Equals(value ?? "", Self, StringComparison.Ordinal);

        /// <summary>是不是类别引用（`@xxx`，且不是 @self）。</summary>
        public static bool IsCategory(string value) =>
            !string.IsNullOrEmpty(value) &&
            value.StartsWith(Prefix, StringComparison.Ordinal) &&
            !IsSelf(value);

        /// <summary>`@rift` → `rift`；不是类别引用时返回 ""。</summary>
        public static string CategoryName(string value) =>
            IsCategory(value) ? value.Substring(Prefix.Length) : "";

        /// <summary>把 `@self` 换成被点的目标 id，其它值原样返回。</summary>
        public static string ResolveSelf(string value, PuzzleContext context) =>
            IsSelf(value) ? (context != null ? context.TargetId ?? "" : "") : value;

        /// <summary>类别的显示写法：`rift` → `@rift`。</summary>
        public static string CategoryRef(string category) => Prefix + (category ?? "");
    }

    /// <summary>时代引用的解算：把 EraRef 变成"这一时刻的那个时代"。</summary>
    public static class PuzzleEraRef
    {
        public static EraId Resolve(EraRef eraRef, PuzzleContext context, EraId absolute)
        {
            EraId target = context != null ? context.TargetEra : absolute;

            switch (eraRef)
            {
                case EraRef.TargetEra: return target;
                case EraRef.TargetEraNext: return EraCatalog.Next(target);
                case EraRef.TargetEraPrevious: return EraCatalog.Previous(target);
                default: return absolute;
            }
        }

        /// <summary>表单 / 日志里的说法。</summary>
        public static string Text(EraRef eraRef)
        {
            switch (eraRef)
            {
                case EraRef.TargetEra: return "被点目标所在的时代";
                case EraRef.TargetEraNext: return "它所在时代的下一个";
                case EraRef.TargetEraPrevious: return "它所在时代的上一个";
                default: return "指定时代";
            }
        }
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

        /// <summary>被点的目标属于哪个类别（prop / character / rift…）。条件里的 `@类别` 目标和它比。</summary>
        public string TargetCategory = "";

        /// <summary>被点的目标现在在哪个时代。人物问人物模型，物体问登记表。</summary>
        public EraId TargetEra = EraId.Stone;

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

    /// <summary>
    /// 规则文案里的占位符。
    ///
    /// 一条规则现在要管一批目标（"任意人物""任意裂隙"），所以那句话不能再写死名字，
    /// 得能引用**这一次点的是谁**。八个占位符，写在任何一句给玩家看的话里都能用：
    ///
    ///   {目标}    被点的那个东西的 id（rift_stone）
    ///   {目标名}  它的显示名（时间裂隙）
    ///   {人物}    当前点名的人物 id
    ///   {人物名}  他的显示名（阿岩）
    ///   {这里}    被点的目标所在时代（石器时代）
    ///   {下一个}  它所在时代的下一个（蒸汽时代）
    ///   {上一个}  它所在时代的上一个
    ///
    /// 认不出来的花括号原样留着（当普通文字），所以写 JSON 之类的东西不会被吃掉。
    /// </summary>
    public static class PuzzleText
    {
        public static string Format(string template, PuzzleContext context)
        {
            if (string.IsNullOrEmpty(template) || context == null || template.IndexOf('{') < 0)
            {
                return template ?? "";
            }

            string text = template;

            text = Replace(text, "{目标名}", NameOf(context, context.TargetId));
            text = Replace(text, "{目标说}", Speech(context, context.TargetId));
            text = Replace(text, "{目标}", context.TargetId);
            text = Replace(text, "{人物名}", NameOf(context, SelectedCharacter(context)));
            text = Replace(text, "{人物说}", Speech(context, SelectedCharacter(context)));
            text = Replace(text, "{人物}", SelectedCharacter(context));
            text = Replace(text, "{这里}", EraTitle(context.TargetEra));
            text = Replace(text, "{下一个}", EraTitle(EraCatalog.Next(context.TargetEra)));
            text = Replace(text, "{上一个}", EraTitle(EraCatalog.Previous(context.TargetEra)));

            return text;
        }

        private static string SelectedCharacter(PuzzleContext context) =>
            context.Characters != null ? context.Characters.SelectedCharacter.Value ?? "" : "";

        private static string NameOf(PuzzleContext context, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "";
            }

            string name = context.State != null ? context.State.GetTargetDisplayName(id) : "";
            return string.IsNullOrEmpty(name) ? id : name;
        }

        private static string Speech(PuzzleContext context, string id) =>
            context.State != null && !string.IsNullOrEmpty(id) ? context.State.GetTargetSpeech(id) : "";

        private static string EraTitle(EraId era) => EraCatalog.Get(era).title;

        private static string Replace(string text, string token, string value) =>
            text.Replace(token, value ?? "");
    }
}
