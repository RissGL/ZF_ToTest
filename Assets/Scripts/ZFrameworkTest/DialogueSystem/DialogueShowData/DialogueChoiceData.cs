using System;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 分支选项：一个按钮 + 跳转目标组。
    /// 挂在 DialogueShowGroupData.choices 上：组内台词播完 → 弹出这些选项 → 玩家点哪个就进哪个组。
    /// </summary>
    [Serializable]
    public class DialogueChoiceData
    {
        [Label("选项文本")]
        public string text;

        [Label("跳转到组 id")]
        public string targetGroupId;

        // 以后要做「无选项自动分叉」（满足某条件跳 A，否则跳 B）：
        // 在这里加 conditionKey / conditionValue，
        // Model 里把「等玩家点」换成「按顺序取第一个满足条件的」，图和数据格式都不用动。
    }
}
