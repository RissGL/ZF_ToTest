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

            return PuzzleRuleMatcher.EvaluateAll(conditions, BuildContext(targetId, Verb.Any, ""));
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

            // 匹配逻辑只有一份实现（PuzzleRuleMatcher），编辑器里的「试跑」用的是同一个
            InteractionRule rule = PuzzleRuleMatcher.SelectRule(
                m_Table.interactionRules, targetId, context.TargetCategory, verb, itemId, context,
                out string fallbackFeedback);

            if (rule != null)
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

            // 兜底提示分两级（都不用单拉一条规则出来）：
            //   ① 规则上的 elseFeedback：「匹配上了但条件不满足」时说的话（更准）
            //   ② 物体上写的「默认提示」：压根没有任何规则命中时说的话
            if (!string.IsNullOrEmpty(fallbackFeedback))
            {
                EmitFeedback(targetId, fallbackFeedback, context);
            }
            else
            {
                string own = this.GetModel<IPuzzleModel>()?.GetDefaultFeedback(targetId);
                if (!string.IsNullOrEmpty(own))
                {
                    EmitFeedback(targetId, own, context);
                }
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

        private bool HasMatchingRule(string targetId, Verb verb, string itemId) =>
            PuzzleRuleMatcher.HasMatch(m_Table.interactionRules, targetId,
                this.GetModel<IPuzzleModel>()?.GetTargetCategory(targetId) ?? "", verb, itemId);

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

                    // 步骤式：走步骤清单（做一步勾一步，勾过的记死）
                    if (definition.mode == PuzzleMode.Steps)
                    {
                        if (CheckSteps(definition, model))
                        {
                            SolvePuzzle(definition.id);
                        }

                        continue;
                    }

                    // 条件式：没写条件的谜题不会自己完成（只能被 SolvePuzzleEffect 显式完成），
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

        /// <summary>
        /// 步骤式谜题：扫一遍步骤清单，把新完成的步骤勾上，返回"必做步骤是否全做完"。
        ///
        /// 两个要点：
        ///   ① **勾过的不退回** —— 第一步"拿到燧石"做完了，后来燧石被用掉，这一步照样算完成
        ///      （不然玩家做过的进度会自己倒退，体验很糟）。
        ///   ② 要按顺序时，前面有必做步骤没完成，后面的步骤就算条件成立也**不给勾**
        ///      （可选步骤不挡路，也不被挡）。
        /// </summary>
        private bool CheckSteps(PuzzleDefinition definition, IPuzzleModel model)
        {
            if (definition.steps == null || definition.steps.Count == 0)
            {
                return false;
            }

            bool allRequiredDone = true;
            bool blocked = false;
            int requiredTotal = 0;
            int stepTotal = 0;

            for (int i = 0; i < definition.steps.Count; i++)
            {
                PuzzleStep step = definition.steps[i];
                if (step == null || string.IsNullOrEmpty(step.id))
                {
                    continue;
                }

                stepTotal++;

                if (!step.optional)
                {
                    requiredTotal++;
                }

                bool done = model.IsStepDone(definition.id, step.id);

                if (!done && (!definition.stepsInOrder || !blocked) && EvaluateAll(step.conditions))
                {
                    model.MarkStepDone(definition.id, step.id);
                    done = true;

                    ExecuteEffects(step.onCompleted, BuildContext("", Verb.Any, ""));

                    definition.CountSteps(out int total, out int finished, model);
                    this.SendEvent(new PuzzleStepCompletedEvent
                    {
                        PuzzleId = definition.id,
                        PuzzleTitle = definition.title,
                        StepId = step.id,
                        StepTitle = step.title,
                        StepNumber = i + 1,
                        StepTotal = definition.steps.Count,
                        RequiredDone = finished,
                        RequiredTotal = total,
                    });

                    Debug.Log($"[谜题] 「{Title(definition)}」第 {i + 1} 步完成：" +
                              $"{(string.IsNullOrEmpty(step.title) ? step.id : step.title)}" +
                              $"（必做 {finished}/{total}）");
                }

                if (!done && !step.optional)
                {
                    allRequiredDone = false;
                    blocked = true;
                }
            }

            return requiredTotal > 0 && allRequiredDone;
        }

        private static string Title(PuzzleDefinition definition) =>
            string.IsNullOrEmpty(definition.title) ? definition.id : definition.title;

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
            ICharacterModel characters = this.GetModel<ICharacterModel>();
            IEraWindowModel eraModel = this.GetModel<IEraWindowModel>();

            string target = targetId ?? "";

            // 「被点的这个在哪个时代、属于哪一类」：
            // 人物问人物模型（人是会走的），物体问登记表（场景里的组件在 Awake 登记过）。
            bool isCharacter = characters != null && characters.IsKnown(target);

            return new PuzzleContext
            {
                TargetId = target,
                TargetCategory = isCharacter
                    ? PuzzleCategories.Character
                    : (model != null ? model.GetTargetCategory(target) : ""),
                TargetEra = isCharacter
                    ? characters.GetEra(target)
                    : (model != null ? model.GetTargetEra(target) : EraId.Stone),
                Verb = verb,
                UsedItemId = itemId ?? "",
                State = model,
                Characters = characters,
                CharacterOps = this.GetArchitecture().GetSystem<ICharacterSystem>(),
                FocusedEraIndex = eraModel != null ? eraModel.FocusedIndex.Value : -1,
                Feedback = message => EmitFeedback(target, message, null),
                SolvePuzzle = SolvePuzzle,
            };
        }

        /// <summary>
        /// 发一句反馈。文案里的占位符（{"{目标名}"} / {"{下一个}"}…）在这里替换 ——
        /// 规则要能一条管一批目标，那句话就不能写死名字。
        /// </summary>
        private void EmitFeedback(string targetId, string message, PuzzleContext context = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string text = context != null
                ? PuzzleText.Format(message, context)
                : PuzzleText.Format(message, BuildContext(targetId, Verb.Any, ""));

            this.SendEvent(new PuzzleFeedbackEvent
            {
                TargetId = targetId,
                Message = text,
            });

            // UI 还没做，先打在 Console 里 —— 至少能看出谜题逻辑是通的
            Debug.Log($"[谜题] {text}");
        }
    }
}
