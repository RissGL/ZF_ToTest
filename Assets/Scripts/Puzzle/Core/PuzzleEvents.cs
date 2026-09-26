using ZGameFramework.Core;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>玩家点了某个物体（不管有没有规则命中）。给演出/音效/统计用。</summary>
    public class PuzzleInteractionEvent : GameEvent
    {
        public string TargetId;
        public Verb Verb;
        public string UsedItemId;

        /// <summary>有没有规则真的生效了。</summary>
        public bool Matched;

        /// <summary>生效那条规则的备注（没命中就是空）。</summary>
        public string RuleNote;
    }

    /// <summary>给玩家的一句反馈文本（"锅炉是冷的，得有个火种"）。UI 还没做，现在只进 Console。</summary>
    public class PuzzleFeedbackEvent : GameEvent
    {
        public string TargetId;
        public string Message;
    }

    /// <summary>一个谜题解开了。</summary>
    public class PuzzleSolvedEvent : GameEvent
    {
        public string PuzzleId;
        public string Title;
        public EraId Era;

        /// <summary>是不是「这个时代的主线谜题」（是的话 EraGallery 那边会把这个时代标记成通关）。</summary>
        public bool IsMain;
    }

    /// <summary>
    /// 步骤式谜题：**刚刚完成了其中一步**（不是整个谜题）。
    /// UI 想显示「2/3」、想播"做完了"的音效、想弹提示，都挂这个。
    /// </summary>
    public class PuzzleStepCompletedEvent : GameEvent
    {
        public string PuzzleId;
        public string PuzzleTitle;
        public string StepId;
        public string StepTitle;

        /// <summary>这一步在清单里是第几步（从 1 开始）。</summary>
        public int StepNumber;
        public int StepTotal;

        /// <summary>必做步骤做完了几步 / 一共几步。</summary>
        public int RequiredDone;
        public int RequiredTotal;
    }

    /// <summary>背包变了。</summary>
    public class PuzzleItemEvent : GameEvent
    {
        public string ItemId;

        /// <summary>true = 拿到，false = 用掉/失去。</summary>
        public bool Gained;
    }

    /// <summary>手里选中的道具变了（"" = 空手）。</summary>
    public class PuzzleSelectionEvent : GameEvent
    {
        public string ItemId;
    }
}
