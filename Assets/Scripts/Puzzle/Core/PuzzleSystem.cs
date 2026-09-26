using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZGameFramework.Core;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 谜题的规则层：查规则表、判条件、执行效果、判定谜题完成。
    /// 它不认识 MonoBehaviour，也不知道场景里摆了什么 —— 只认 id。
    /// </summary>
    public interface IPuzzleSystem : ISystem
    {
        PuzzleTableSO Table { get; }

        /// <summary>绑规则表（由 PuzzleBootstrap 在 Awake 里给）。</summary>
        void SetTable(PuzzleTableSO table);

        /// <summary>这组条件现在全部满足吗。物体的 visualRules 也靠它判。</summary>
        bool EvaluateAll(IReadOnlyList<PuzzleCondition> conditions, string targetId = "");

        /// <summary>玩家点了某个物体。手里有道具就先试「用道具」，没有匹配的规则再退回空手点。</summary>
        void Interact(string targetId);

        void Interact(string targetId, Verb verb, string itemId);

        /// <summary>算出物体现在该是哪个状态：先看 visualRules，没命中再用它自己的物体状态，最后退回默认值。</summary>
        string ResolveVisualState(string interactableId, IReadOnlyList<VisualStateRule> visualRules, string defaultState);

        /// <summary>重新检查所有谜题的完成条件（状态每变一次都会调）。</summary>
        void RecheckPuzzles();

        /// <summary>完成一个谜题：跑它的完成效果、发事件、主线谜题顺便判定时代通关。</summary>
        void SolvePuzzle(string puzzleId);

        void ResetAll();
    }

    public class PuzzleSystem : AbstractSystem, IPuzzleSystem
    {
        private PuzzleTableSO m_Table;
        private bool m_Rechecking;
        private bool m_RecheckAgain;

        public PuzzleTableSO Table => m_Table;

        protected override void OnInit()
        {
        }

        public void SetTable(PuzzleTableSO table)
        {
            m_Table = table;

            if (m_Table == null)
            {
                Debug.LogError("[谜题] 规则表是空的。跑一下菜单 Tools/谜题/搭建解密演示，或手动填 PuzzleBootstrap 上的表。");
                return;
            }

            // 开局先把已经满足条件的谜题认掉（读档回来、或者规则表里天然成立的）
            RecheckPuzzles();
        }

        // ===================== 条件 =====================

        public bool EvaluateAll(IReadOnlyList<PuzzleCondition> conditions, string targetId = "")
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            PuzzleContext context = BuildContext(targetId, Verb.Any, "");
            for (int i = 0; i < conditions.Count; i++)
            {
                PuzzleCondition condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                if (!condition.IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }

        // ===================== 交互 =====================

        public void Interact(string targetId) => Interact(targetId, Verb.Any, "");

        public void Interact(string targetId, Verb verb, string itemId)
        {
            if (m_Table == null)
            {
                Debug.LogError("[谜题] 规则表还没绑上，交互被丢掉了。");
                return;
            }

            if (verb == Verb.Any)
            {
                // 手里拿着道具：先看有没有「用这个道具」的规则，一条都没匹配上才退回空手点。
                // 这样玩家不用去分辨"这个该用道具还是该空手点"，点了就有反馈。
                IPuzzleModel model = this.GetModel<IPuzzleModel>();
                string selected = model != null ? model.SelectedItem.Value : "";

                if (!string.IsNullOrEmpty(selected) && HasMatchingRule(targetId, Verb.UseItem, selected))
                {
                    RunRules(targetId, Verb.UseItem, selected);
                    return;
                }

                RunRules(targetId, Verb.Interact, "");
                return;
            }

            RunRules(targetId, verb, itemId);
        }

        private bool RunRules(string targetId, Verb verb, string itemId)
        {
            PuzzleContext context = BuildContext(targetId, verb, itemId);
            string pendingFeedback = null;

            List<InteractionRule> rules = m_Table.interactionRules;
            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (rule == null || !MatchesTarget(rule, targetId, verb, itemId))
                {
                    continue;
                }

                context.Rule = rule;

                if (EvaluateAll(rule.conditions, targetId))
                {
                    ExecuteEffects(rule.effects, context);
                    this.SendEvent(new PuzzleInteractionEvent
                    {
                        TargetId = targetId,
                        Verb = verb,
                        UsedItemId = itemId,
                        Matched = true,
                        RuleNote = rule.note,
                    });
                    return true;
                }

                // 记住第一条"匹配了但条件不满足"的提示，全部试完都没命中时用它
                if (pendingFeedback == null && !string.IsNullOrEmpty(rule.elseFeedback))
                {
                    pendingFeedback = rule.elseFeedback;
                }
            }

            if (!string.IsNullOrEmpty(pendingFeedback))
            {
                EmitFeedback(targetId, pendingFeedback);
            }

            this.SendEvent(new PuzzleInteractionEvent
            {
                TargetId = targetId,
                Verb = verb,
                UsedItemId = itemId,
                Matched = false,
            });
            return false;
        }

        private bool HasMatchingRule(string targetId, Verb verb, string itemId)
        {
            List<InteractionRule> rules = m_Table.interactionRules;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && MatchesTarget(rules[i], targetId, verb, itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesTarget(InteractionRule rule, string targetId, Verb verb, string itemId)
        {
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

        // ===================== 效果 =====================

        private void ExecuteEffects(List<PuzzleEffect> effects, PuzzleContext context)
        {
            if (effects == null || effects.Count == 0)
            {
                AfterStateChanged();
                return;
            }

            bool needsDelay = false;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null && effects[i].delayBefore > 0.0001f)
                {
                    needsDelay = true;
                    break;
                }
            }

            if (!needsDelay)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    effects[i]?.Execute(context);
                }

                AfterStateChanged();
                return;
            }

            // 有等待的效果：System 是纯逻辑没有 Update，借框架的 MonoManager 跑协程
            MonoManager.Instance.StartCoroutine(RunEffectsWithDelay(effects, context));
        }

        private IEnumerator RunEffectsWithDelay(List<PuzzleEffect> effects, PuzzleContext context)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                PuzzleEffect effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                if (effect.delayBefore > 0.0001f)
                {
                    yield return new WaitForSeconds(effect.delayBefore);
                }

                effect.Execute(context);
            }

            AfterStateChanged();
        }

        private void AfterStateChanged() => RecheckPuzzles();

        // ===================== 谜题完成 =====================

        public void RecheckPuzzles()
        {
            if (m_Table == null)
            {
                return;
            }

            // 完成效果里可能又改状态、又解开别的谜题 —— 用"再跑一轮"代替递归，免得链长了爆栈
            if (m_Rechecking)
            {
                m_RecheckAgain = true;
                return;
            }

            IPuzzleModel model = this.GetModel<IPuzzleModel>();
            if (model == null)
            {
                return;
            }

            m_Rechecking = true;
            do
            {
                m_RecheckAgain = false;

                List<PuzzleDefinition> puzzles = m_Table.puzzles;
                for (int i = 0; i < puzzles.Count; i++)
                {
                    PuzzleDefinition definition = puzzles[i];
                    if (definition == null || string.IsNullOrEmpty(definition.id))
                    {
                        continue;
                    }

                    if (model.IsSolved(definition.id))
                    {
                        continue;
                    }

                    // 没写条件的谜题不会自己完成（只能被 SolvePuzzleEffect 显式完成），
                    // 否则一个手滑忘了填条件就会开局自动通关
                    if (definition.conditions == null || definition.conditions.Count == 0)
                    {
                        continue;
                    }

                    if (!EvaluateAll(definition.conditions))
                    {
                        continue;
                    }

                    SolvePuzzle(definition.id);
                }
            }
            while (m_RecheckAgain);

            m_Rechecking = false;
        }

        public void SolvePuzzle(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
            {
                return;
            }

            IPuzzleModel model = this.GetModel<IPuzzleModel>();
            if (model == null || model.IsSolved(puzzleId))
            {
                return;
            }

            PuzzleDefinition definition = m_Table != null ? m_Table.FindPuzzle(puzzleId) : null;
            if (definition == null)
            {
                Debug.LogWarning($"[谜题] 规则表里没有 id 为「{puzzleId}」的谜题，只标记了完成状态。");
            }

            model.MarkSolved(puzzleId);

            if (definition != null)
            {
                ExecuteEffects(definition.onSolved, BuildContext("", Verb.Any, ""));
            }

            this.SendEvent(new PuzzleSolvedEvent
            {
                PuzzleId = puzzleId,
                Title = definition != null ? definition.title : puzzleId,
                Era = definition != null ? definition.era : EraId.Stone,
                IsMain = definition != null && definition.isMainPuzzle,
            });

            // 主线谜题解开 = 这个时代通关了。
            // 这里用 (int)EraId 当窗口序号 —— EraWorldController 是按 EraId 排序后写序号的，同一个约定。
            // 以后时代不再和窗口一一对应，就在这儿换成查一次映射。
            if (definition != null && definition.isMainPuzzle)
            {
                this.SendCommand(new CompleteEraCommand((int)definition.era));
            }
        }

        public void ResetAll()
        {
            this.GetModel<IPuzzleModel>()?.ResetAll();
            RecheckPuzzles();
        }

        // ===================== 物体状态 =====================

        public string ResolveVisualState(string interactableId, IReadOnlyList<VisualStateRule> visualRules, string defaultState)
        {
            if (visualRules != null)
            {
                for (int i = 0; i < visualRules.Count; i++)
                {
                    VisualStateRule rule = visualRules[i];
                    if (rule == null)
                    {
                        continue;
                    }

                    if (EvaluateAll(rule.conditions, interactableId))
                    {
                        return rule.state;
                    }
                }
            }

            IPuzzleModel model = this.GetModel<IPuzzleModel>();
            string own = model != null ? model.GetObjectState(interactableId) : "";
            if (!string.IsNullOrEmpty(own))
            {
                return own;
            }

            return string.IsNullOrEmpty(defaultState) ? PuzzleStates.Default : defaultState;
        }

        // ===================== 杂项 =====================

        private PuzzleContext BuildContext(string targetId, Verb verb, string itemId)
        {
            IPuzzleModel model = this.GetModel<IPuzzleModel>();
            IEraWindowModel eraModel = this.GetModel<IEraWindowModel>();

            string target = targetId ?? "";

            return new PuzzleContext
            {
                TargetId = target,
                Verb = verb,
                UsedItemId = itemId ?? "",
                State = model,
                FocusedEraIndex = eraModel != null ? eraModel.FocusedIndex.Value : -1,
                Feedback = message => EmitFeedback(target, message),
                SolvePuzzle = SolvePuzzle,
            };
        }

        private void EmitFeedback(string targetId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            this.SendEvent(new PuzzleFeedbackEvent
            {
                TargetId = targetId,
                Message = message,
            });

            // UI 还没做，先打在 Console 里 —— 至少能看出谜题逻辑是通的
            Debug.Log($"[谜题] {message}");
        }
    }
}
