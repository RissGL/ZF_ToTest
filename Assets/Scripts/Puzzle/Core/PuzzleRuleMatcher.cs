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
        /// <summary>这条规则的目标 / 动作对不对得上。</summary>
        public static bool MatchesTarget(InteractionRule rule, string targetId, Verb verb, string itemId)
        {
            if (rule == null)
            {
                return false;
            }

            if (rule.verb != Verb.Any && rule.verb != verb)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(rule.targetId) && rule.targetId != targetId)
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
        public static bool HasMatch(IReadOnlyList<InteractionRule> rules, string targetId, Verb verb, string itemId)
        {
            if (rules == null)
            {
                return false;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                if (MatchesTarget(rules[i], targetId, verb, itemId))
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
        public static InteractionRule SelectRule(IReadOnlyList<InteractionRule> rules, string targetId, Verb verb,
            string itemId, PuzzleContext context, out string fallbackFeedback)
        {
            fallbackFeedback = null;

            if (rules == null)
            {
                return null;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (!MatchesTarget(rule, targetId, verb, itemId))
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
