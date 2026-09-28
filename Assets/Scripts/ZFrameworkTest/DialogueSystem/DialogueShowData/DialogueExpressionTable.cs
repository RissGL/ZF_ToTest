using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一个表情的完整定义：枚举值 / 中文显示名 / 是不是漫画夸张表情 / 从角色资产上取哪张图。
    /// </summary>
    public sealed class DialogueExpressionInfo
    {
        public readonly DialogueCharExpressionEnum Expression;

        /// <summary>中文名（编辑器下拉、日志用）</summary>
        public readonly string DisplayName;

        /// <summary>是不是漫画夸张表情（爆点用；用来做"突然切一格画风"的判定）</summary>
        public readonly bool IsComic;

        /// <summary>从角色资产上取这张表情图（没配返回 null，由 DialogueSpeaker 兜底到平静）</summary>
        public readonly Func<DialogueSpeaker, Sprite> GetFace;

        public DialogueExpressionInfo(DialogueCharExpressionEnum expression, string displayName, bool isComic,
            Func<DialogueSpeaker, Sprite> getFace)
        {
            Expression = expression;
            DisplayName = displayName;
            IsComic = isComic;
            GetFace = getFace;
        }
    }

    /// <summary>
    /// 表情表 —— **加一个表情只改两个地方**：
    ///   ① <see cref="DialogueCharExpressionEnum"/> 加一个成员（只加不改：数据里存的是 int）
    ///   ② 本表的 <see cref="All"/> 加一条（中文名 / 是否漫画 / 从角色资产的哪个字段取图）
    ///      + <see cref="DialogueSpeaker"/> 加一个对应的 Sprite 字段
    ///
    /// 为什么要有这张表：以前加一个表情要同步三处 ——
    /// 枚举、`DialogueSpeaker.Pick` 的 switch、`DialogueExpressionUtil.ToDisplayName` 的 switch，
    /// 漏一处就是"下拉里能选，但选了什么图都不换"或者日志里显示英文。
    /// 现在取图 / 中文名 / 是否漫画都从这一张表走，漏了会在 Console 直接报"哪个表情没登记"。
    /// </summary>
    public static class DialogueExpressionTable
    {
        /// <summary>全部表情（顺序 = 枚举顺序，编辑器下拉也按它排）</summary>
        public static readonly DialogueExpressionInfo[] All =
        {
            // ---- 常规情绪 ----
            E(DialogueCharExpressionEnum.defaultFace, false, s => s.defaultFace),
            E(DialogueCharExpressionEnum.Happy, false, s => s.happyFace),
            E(DialogueCharExpressionEnum.Sad, false, s => s.sadFace),
            E(DialogueCharExpressionEnum.Angry, false, s => s.angryFace),
            E(DialogueCharExpressionEnum.Surprise, false, s => s.surpriseFace),
            E(DialogueCharExpressionEnum.Fear, false, s => s.fearFace),
            E(DialogueCharExpressionEnum.Shy, false, s => s.shyFace),
            E(DialogueCharExpressionEnum.Worry, false, s => s.worryFace),
            E(DialogueCharExpressionEnum.Think, false, s => s.thinkFace),
            E(DialogueCharExpressionEnum.Serious, false, s => s.seriousFace),

            // ---- 漫画夸张表情（爆点：建议配合换模板 / 换气泡样式）----
            E(DialogueCharExpressionEnum.Shock, true, s => s.shockFace),
            E(DialogueCharExpressionEnum.Dark, true, s => s.darkFace),
            E(DialogueCharExpressionEnum.Sweat, true, s => s.sweatFace),
            E(DialogueCharExpressionEnum.Sparkle, true, s => s.sparkleFace),
            E(DialogueCharExpressionEnum.Dizzy, true, s => s.dizzyFace),
            E(DialogueCharExpressionEnum.Panic, true, s => s.panicFace),
            E(DialogueCharExpressionEnum.Rage, true, s => s.rageFace),
            E(DialogueCharExpressionEnum.Comic, true, s => s.comicFace),
        };

        private static readonly Dictionary<DialogueCharExpressionEnum, DialogueExpressionInfo> s_Map = BuildMap();
        private static bool m_Validated;

        /// <summary>这条表情的登记（没登记返回 null，调用方兜底）</summary>
        public static DialogueExpressionInfo Find(DialogueCharExpressionEnum expression)
        {
            EnsureValidated();
            return s_Map.TryGetValue(expression, out var info) ? info : null;
        }

        /// <summary>从角色资产上取这张图（没配/没登记返回 null，不兜底）</summary>
        public static Sprite GetFace(DialogueSpeaker speaker, DialogueCharExpressionEnum expression)
        {
            if (speaker == null)
            {
                return null;
            }

            var info = Find(expression);
            return info?.GetFace != null ? info.GetFace(speaker) : null;
        }

        /// <summary>中文显示名（没登记就退回枚举名）</summary>
        public static string DisplayName(DialogueCharExpressionEnum expression)
        {
            var info = Find(expression);
            return info != null ? info.DisplayName : expression.ToString();
        }

        /// <summary>是不是漫画夸张表情</summary>
        public static bool IsComic(DialogueCharExpressionEnum expression)
        {
            var info = Find(expression);
            return info != null && info.IsComic;
        }

        /// <summary>
        /// 枚举里每个成员都必须在表里登记（少一个就报哪个少了）——
        /// 这是"加表情漏一处"最后的防线。
        /// </summary>
        public static void EnsureValidated()
        {
            if (m_Validated)
            {
                return;
            }

            m_Validated = true;

            foreach (DialogueCharExpressionEnum value in Enum.GetValues(typeof(DialogueCharExpressionEnum)))
            {
                if (!s_Map.ContainsKey(value))
                {
                    Debug.LogError($"[对话表情] 表情「{value}」在 DialogueExpressionTable.All 里没有登记：" +
                                   "加表情要同时改枚举、本表和 DialogueSpeaker 的 Sprite 字段");
                }
            }
        }

        // ===================== 内部 =====================

        /// <summary>登记一条：中文名直接从枚举成员上的 [InspectorName] 读，不在这里再抄一遍</summary>
        private static DialogueExpressionInfo E(DialogueCharExpressionEnum expression, bool isComic,
            Func<DialogueSpeaker, Sprite> getFace)
        {
            return new DialogueExpressionInfo(expression, ResolveDisplayName(expression), isComic, getFace);
        }

        private static Dictionary<DialogueCharExpressionEnum, DialogueExpressionInfo> BuildMap()
        {
            var map = new Dictionary<DialogueCharExpressionEnum, DialogueExpressionInfo>();

            foreach (var info in All)
            {
                if (map.ContainsKey(info.Expression))
                {
                    Debug.LogError($"[对话表情] 表情表里「{info.Expression}」登记了两次，后面的那条被忽略");
                    continue;
                }

                map.Add(info.Expression, info);
            }

            return map;
        }

        /// <summary>中文名取枚举成员上的 [InspectorName("…")]（编辑器下拉也用它），没有就退回成员名</summary>
        private static string ResolveDisplayName(DialogueCharExpressionEnum expression)
        {
            var field = typeof(DialogueCharExpressionEnum).GetField(expression.ToString());
            var attribute = field?.GetCustomAttribute<InspectorNameAttribute>();

            if (attribute != null && !string.IsNullOrEmpty(attribute.displayName))
            {
                return attribute.displayName;
            }

            return expression.ToString();
        }
    }
}
