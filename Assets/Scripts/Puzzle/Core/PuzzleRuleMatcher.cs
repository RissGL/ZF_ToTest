using System.Collections.Generic;

namespace ZF.Puzzle
{
    /// <summary>
    /// 规则匹配的**唯一**实现。
    ///
    /// 运行时（PuzzleSystem）和编辑器里的「试跑」都走这里 ——
    /// 工具要是自己另写一套匹配逻辑，算出来的结果和真跑起来不一样，那工具就没意义了。
    /// </summary>
    public static class PuzzleRuleMatcher
    {
        /// <summary>
        /// 这条规则的目标 / 动作对不对得上。
        /// targetCategory = 被点的那个目标属于哪个类别（规则写 `@rift` 时靠它匹配）。
        /// </summary>
        public static bool MatchesTarget(InteractionRule rule, string targetId, string targetCategory,
            Verb verb, string itemId)
        {
            if (rule == null)
            {
                return false;
            }

            if (rule.verb != Verb.Any && rule.verb != verb)
            {
                return false;
            }

            if (!TargetMatches(rule.targetId, targetId, targetCategory))
            {
                return false;
            }

            // 规则指定了道具，那这条规则就只认「手里拿着那个道具去点」
            if (!string.IsNullOrEmpty(rule.itemId) && (verb != Verb.UseItem || rule.itemId != itemId))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 目标匹配：留空 = 任意；`@类别` = 目标属于这个类别；其它 = 具体 id。
        /// 类别是**一次管一批**的关键（`@rift` 一条规则管四个裂隙，`@character` 一条管五个人物）。
        /// </summary>
        public static bool TargetMatches(string ruleTarget, string targetId, string targetCategory)
        {
            if (string.IsNullOrEmpty(ruleTarget))
            {
                return true;
            }

            if (PuzzleRef.IsCategory(ruleTarget))
            {
                return PuzzleOps.SameState(PuzzleRef.CategoryName(ruleTarget), targetCategory);
            }

            return PuzzleOps.SameState(ruleTarget, targetId);
        }

        /// <summary>这组条件现在全部满足吗。空列表 = 无条件 = 通过。</summary>
        public static bool EvaluateAll(IReadOnlyList<PuzzleCondition> conditions, PuzzleContext context)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                PuzzleCondition condition = conditions[i];
                if (condition != null && !condition.IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>有没有任何一条规则的目标 + 动作对得上（不看条件）。</summary>
        public static bool HasMatch(IReadOnlyList<InteractionRule> rules, string targetId, string targetCategory,
            Verb verb, string itemId)
        {
            if (rules == null)
            {
                return false;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                if (MatchesTarget(rules[i], targetId, targetCategory, verb, itemId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从上往下挑第一条「目标 + 动作匹配 **且** 条件全满足」的规则。
        /// 一条都没命中时，把第一条「匹配但条件不满足」的规则的 elseFeedback 通过 fallbackFeedback 带出来。
        /// </summary>
        public static InteractionRule SelectRule(IReadOnlyList<InteractionRule> rules, string targetId,
            string targetCategory, Verb verb, string itemId, PuzzleContext context, out string fallbackFeedback)
        {
            fallbackFeedback = null;

            if (rules == null)
            {
                return null;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (!MatchesTarget(rule, targetId, targetCategory, verb, itemId))
                {
                    continue;
                }

                context.Rule = rule;

                if (EvaluateAll(rule.conditions, context))
                {
                    return rule;
                }

                if (fallbackFeedback == null && !string.IsNullOrEmpty(rule.elseFeedback))
                {
                    fallbackFeedback = rule.elseFeedback;
                }
            }

            return null;
        }
    }
}
