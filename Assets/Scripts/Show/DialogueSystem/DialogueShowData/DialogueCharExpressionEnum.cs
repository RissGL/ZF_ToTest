using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 立绘表情。
    ///
    /// 分两类：
    ///   ① **常规情绪**（上半段）：日常对话用，一张脸一个情绪
    ///   ② **漫画夸张表情**（下半段，从 Shock 开始）：漫画分镜的爆点用 ——
    ///      突然切一格完全不同的画风（冲击线 / 阴影脸 / 冒汗 / Q 版…）。
    ///      这类一般**配合换模板（templateId）或换气泡样式**用，才有"突然来一格"的效果。
    ///
    /// ★ 改枚举名会让已导出数据里的数字错位（数据里存的是 int），**只加不改**。
    /// ★ 加一个表情要改三处：本枚举 + <see cref="DialogueExpressionTable"/>（登记"从角色资产哪个字段取图"、
    ///   是不是漫画类）+ <see cref="DialogueSpeaker"/>（加一个 Sprite 字段）。
    ///   中文名写在本文件每个成员头上的 `[InspectorName]`，编辑器下拉和日志都用它，不用再抄一遍。
    /// </summary>
    public enum DialogueCharExpressionEnum
    {
        // ===================== 常规情绪 =====================

        /// <summary>平静（默认表情，其它表情没配图时都回退到它）</summary>
        [InspectorName("平静")]
        defaultFace,

        /// <summary>高兴</summary>
        [InspectorName("高兴")]
        Happy,

        /// <summary>难过</summary>
        [InspectorName("难过")]
        Sad,

        /// <summary>生气</summary>
        [InspectorName("生气")]
        Angry,

        /// <summary>惊讶</summary>
        [InspectorName("惊讶")]
        Surprise,

        /// <summary>害怕</summary>
        [InspectorName("害怕")]
        Fear,

        /// <summary>害羞 / 脸红</summary>
        [InspectorName("害羞")]
        Shy,

        /// <summary>为难 / 苦恼</summary>
        [InspectorName("为难")]
        Worry,

        /// <summary>思考</summary>
        [InspectorName("思考")]
        Think,

        /// <summary>认真（下决心的脸）</summary>
        [InspectorName("认真")]
        Serious,

        // ===================== 漫画夸张表情（爆点） =====================

        /// <summary>惊愕：冲击线 / 屏幕裂开</summary>
        [InspectorName("惊愕（漫画）")]
        Shock,

        /// <summary>黑化：脸上一半阴影</summary>
        [InspectorName("黑化（漫画）")]
        Dark,

        /// <summary>冒汗：无语 / 尴尬</summary>
        [InspectorName("冒汗（漫画）")]
        Sweat,

        /// <summary>闪亮：星星眼 / 期待</summary>
        [InspectorName("闪亮（漫画）")]
        Sparkle,

        /// <summary>眩晕：混乱 / 转圈</summary>
        [InspectorName("眩晕（漫画）")]
        Dizzy,

        /// <summary>慌乱：惨叫 / 白目</summary>
        [InspectorName("慌乱（漫画）")]
        Panic,

        /// <summary>暴怒：怒符 / 爆青筋</summary>
        [InspectorName("暴怒（漫画）")]
        Rage,

        /// <summary>漫画夸张：Q 版 / 变形</summary>
        [InspectorName("Q 版（漫画）")]
        Comic,
    }

    /// <summary>表情的分类判断（工具、编辑器提示用）—— 真正的数据在 <see cref="DialogueExpressionTable"/> 里</summary>
    public static class DialogueExpressionUtil
    {
        /// <summary>是不是漫画夸张表情（爆点用）</summary>
        public static bool IsComic(DialogueCharExpressionEnum expression)
        {
            return DialogueExpressionTable.IsComic(expression);
        }

        /// <summary>给编辑器 / 日志用的中文名</summary>
        public static string ToDisplayName(DialogueCharExpressionEnum expression)
        {
            return DialogueExpressionTable.DisplayName(expression);
        }
    }
}
