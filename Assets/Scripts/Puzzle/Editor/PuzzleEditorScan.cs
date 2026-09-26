using System.Collections.Generic;
using UnityEngine;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    public class PuzzleIssue
    {
        /// <summary>属于第几条规则（0 基）。-1 = 不属于某条规则。</summary>
        public int RuleIndex = -1;

        /// <summary>true = 一定是错的；false = 可疑 / 提醒。</summary>
        public bool IsError;

        public string Message = "";
    }

    /// <summary>整张表的扫描结果：场景里有哪些 id、flag 谁读谁写、有哪些问题。两个编辑器窗口共用。</summary>
    public class PuzzleScanResult
    {
        /// <summary>场景里所有可交互物 + 人物的 id。</summary>
        public readonly HashSet<string> InteractionIds = new HashSet<string>();

        public readonly HashSet<string> CharacterIds = new HashSet<string>();

        /// <summary>场景里出现过的类别（prop / character / rift…）。规则里的 `@类别` 从这儿挑。</summary>
        public readonly HashSet<string> Categories = new HashSet<string>();

        /// <summary>id → 类别。校验「@类别 到底盖住了谁」和排序提示都要用。</summary>
        public readonly Dictionary<string, string> CategoryOf = new Dictionary<string, string>();

        /// <summary>id → 显示名。给编辑器提示用。</summary>
        public readonly Dictionary<string, string> DisplayNameOf = new Dictionary<string, string>();

        /// <summary>id → 挂在它身上的一句额外文案（规则里的 {目标说}）。</summary>
        public readonly Dictionary<string, string> SpeechOf = new Dictionary<string, string>();

        /// <summary>id → 摆在哪个时代（物体；人物看 homeEra）。</summary>
        public readonly Dictionary<string, EraId> EraOf = new Dictionary<string, EraId>();

        public readonly HashSet<string> WrittenFlags = new HashSet<string>();
        public readonly HashSet<string> ReadFlags = new HashSet<string>();
        public readonly HashSet<string> GivenItems = new HashSet<string>();
        public readonly HashSet<string> UsedItems = new HashSet<string>();

        /// <summary>表里出现过的物体状态名（给编辑器当候选：lit / cold / open…）。</summary>
        public readonly HashSet<string> ObjectStates = new HashSet<string>();

        public readonly List<PuzzleIssue> Issues = new List<PuzzleIssue>();

        public int Errors { get; private set; }
        public int Warnings { get; private set; }

        public void Add(int ruleIndex, bool isError, string message)
        {
            Issues.Add(new PuzzleIssue { RuleIndex = ruleIndex, IsError = isError, Message = message });

            if (isError)
            {
                Errors++;
            }
            else
            {
                Warnings++;
            }
        }

        /// <summary>某条规则身上的问题。</summary>
        public List<PuzzleIssue> IssuesOf(int ruleIndex)
        {
            List<PuzzleIssue> result = new List<PuzzleIssue>();
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].RuleIndex == ruleIndex)
                {
                    result.Add(Issues[i]);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 扫一遍规则表 + 场景，产出「有哪些问题」。
    /// 这些问题是 EditorWindow 里表格和依赖图都要用的，所以抽出来放一处。
    /// </summary>
    public static class PuzzleEditorScan
    {
        /// <summary>引用的三种去处。</summary>
        private enum RefKind
        {
            /// <summary>必须是场景里的物体 / 人物 id。</summary>
            Interaction = 0,

            /// <summary>必须是某个道具（得有人给过它）。</summary>
            Item = 1,

            /// <summary>`@类别` —— 场景里得有东西属于这个类别。</summary>
            Category = 2,

            /// <summary>`@self` —— 被点的那个目标，不用校验。</summary>
            Self = 3,
        }

        private struct Reference
        {
            public string Value;
            public RefKind Kind;
            public int RuleIndex;
            public string Where;
        }

        public static PuzzleScanResult Scan(PuzzleTableSO table)
        {
            PuzzleScanResult result = new PuzzleScanResult();

            CollectSceneIds(result);

            if (table == null)
            {
                result.Add(-1, true, "没有规则表。");
                return result;
            }

            List<Reference> references = new List<Reference>();
            HashSet<string> puzzleRefs = new HashSet<string>();

            ScanRules(table, result, references, puzzleRefs);
            ScanPuzzles(table, result, references, puzzleRefs);

            ValidateReferences(result, references);
            ValidateFlags(result);
            ValidatePuzzles(table, result, puzzleRefs);

            return result;
        }

        // ===================== 场景里的 id =====================

        private static void CollectSceneIds(PuzzleScanResult result)
        {
            StateVisualBehaviour[] views = Object.FindObjectsOfType<StateVisualBehaviour>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] == null)
                {
                    continue;
                }

                string id = views[i].InteractionId;
                if (!string.IsNullOrEmpty(id))
                {
                    result.InteractionIds.Add(id);
                    result.CategoryOf[id] = views[i].Category;
                    result.DisplayNameOf[id] = views[i].DisplayName;
                    result.SpeechOf[id] = views[i].Speech;
                    result.Categories.Add(views[i].Category);
                }

                if (views[i] is CharacterView character)
                {
                    result.CharacterIds.Add(character.CharacterId);
                    result.EraOf[character.CharacterId] = character.HomeEra;
                }
                else if (views[i] is Interactable interactable && !string.IsNullOrEmpty(id))
                {
                    result.EraOf[id] = interactable.Era;
                }
            }
        }

        // ===================== 规则 =====================

        private static void ScanRules(PuzzleTableSO table, PuzzleScanResult result,
            List<Reference> references, HashSet<string> puzzleRefs)
        {
            List<InteractionRule> rules = table.interactionRules;

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (rule == null)
                {
                    result.Add(i, true, "空规则（列表里有个 None）。");
                    continue;
                }

                if (!string.IsNullOrEmpty(rule.targetId))
                {
                    AddTargetRef(references, rule.targetId, i, "规则的目标");
                }
                else if (rule.verb == Verb.UseItem)
                {
                    result.Add(i, true, "动作是「用道具」但目标留空了 —— 通配目标 + 指定道具会抢走太多点击。");
                }

                if (!string.IsNullOrEmpty(rule.itemId))
                {
                    AddItemRef(references, rule.itemId, i, "规则要求用的道具");
                }

                for (int c = 0; c < rule.conditions.Count; c++)
                {
                    WalkCondition(rule.conditions[c], result, references, puzzleRefs, i);
                }

                for (int e = 0; e < rule.effects.Count; e++)
                {
                    WalkEffect(rule.effects[e], result, references, puzzleRefs, i);
                }
            }

            CheckShadowing(rules, result);
            CheckPureFallbackRules(rules, result);
        }

        /// <summary>
        /// 「无条件 + 只有一个提示效果」的兜底规则：它能干的事，其实用一条字段就能干
        /// —— 挂到上一条同类规则的 elseFeedback，或者挂到物体的「默认提示」上。
        /// 这种规则在图里就是一个纯占位节点，多了图会乱死，所以提醒一句。
        /// </summary>
        private static void CheckPureFallbackRules(List<InteractionRule> rules, PuzzleScanResult result)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (!IsOnlyFeedback(rule))
                {
                    continue;
                }

                // 上面有没有同类（同目标同动作）的规则？有的话它就是纯兜底，直接并上去
                int sameTargetAbove = FindCoveringRuleAbove(rules, i, result);

                result.Add(i, false, sameTargetAbove >= 0
                    ? $"这条只是兜底提示，不用单拉一条 —— 把这句话填到第 {sameTargetAbove + 1} 条的「条件不满足时，对玩家说」里就行。"
                    : "这条只是「点它没反应时说一句话」—— 建议改成填在物体自己的「空手点它时的默认提示」上，规则表里就少一行。");
            }
        }

        /// <summary>「无条件 + 效果只有一个提示」—— 这条规则除了说句话什么都没干。</summary>
        public static bool IsOnlyFeedback(InteractionRule rule) =>
            rule != null &&
            rule.conditions != null && rule.conditions.Count == 0 &&
            rule.effects != null && rule.effects.Count == 1 && rule.effects[0] is FeedbackEffect;

        /// <summary>
        /// 第 index 条上面有没有「能匹配到它全部点击」的规则（同目标同动作，认得 `@类别`）。有 → 它就是个纯兜底，
        /// 那句话并到上面那条的 elseFeedback 里就行。演示搭建器判「旧表」用的也是这个判断。
        /// </summary>
        public static int FindCoveringRuleAbove(List<InteractionRule> rules, int index,
            PuzzleScanResult result = null)
        {
            if (rules == null || index < 0 || index >= rules.Count)
            {
                return -1;
            }

            InteractionRule rule = rules[index];
            if (rule == null)
            {
                return -1;
            }

            for (int j = 0; j < index; j++)
            {
                if (Covers(rules[j], rule, result) || Covers(rule, rules[j], result))
                {
                    return j;
                }
            }

            return -1;
        }

        /// <summary>「上面那条无条件规则会不会把它吃干净」—— 规则表最常见的坑就是把兜底写前面了。</summary>
        private static void CheckShadowing(List<InteractionRule> rules, PuzzleScanResult result)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule later = rules[i];
                if (later == null)
                {
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    InteractionRule earlier = rules[j];
                    if (earlier == null || !Covers(earlier, later, result))
                    {
                        continue;
                    }

                    if (earlier.conditions.Count == 0)
                    {
                        result.Add(i, true,
                            $"永远轮不到它：上面第 {j + 1} 条（{Note(earlier)}）是无条件的同类规则，会先命中。兜底规则必须往下放。");
                        break;
                    }

                    if (SameConditions(earlier, later))
                    {
                        result.Add(i, false,
                            $"和上面第 {j + 1} 条条件完全一样，它永远轮不到（留一条就够）。");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// earlier 是否"覆盖"了 later：earlier 能匹配到 later 能匹配的所有点击。
        /// 目标上要理解通配：earlier 留空 = 谁都吃；earlier 是 `@类别` = 吃这个类别里的具体 id。
        /// </summary>
        private static bool Covers(InteractionRule earlier, InteractionRule later, PuzzleScanResult result)
        {
            if (!TargetCovers(earlier.targetId, later.targetId, result))
            {
                return false;
            }

            if (earlier.verb != Verb.Any && earlier.verb != later.verb)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(earlier.itemId) && earlier.itemId != later.itemId)
            {
                return false;
            }

            return true;
        }

        /// <summary>earlierTarget 的匹配范围是不是把 laterTarget 整个包住了。</summary>
        private static bool TargetCovers(string earlierTarget, string laterTarget, PuzzleScanResult result)
        {
            if (string.IsNullOrEmpty(earlierTarget))
            {
                return true;   // 任意目标，什么都吃
            }

            if (PuzzleOps.SameState(earlierTarget, laterTarget))
            {
                return true;
            }

            // earlier = @类别，later = 这个类别里的某个具体 id → 包住了
            if (PuzzleRef.IsCategory(earlierTarget) &&
                !string.IsNullOrEmpty(laterTarget) &&
                !PuzzleRef.IsCategory(laterTarget) &&
                result != null &&
                result.CategoryOf.TryGetValue(laterTarget, out string category))
            {
                return PuzzleOps.SameState(PuzzleRef.CategoryName(earlierTarget), category);
            }

            return false;
        }

        private static bool SameConditions(InteractionRule a, InteractionRule b)
        {
            if (a.conditions.Count != b.conditions.Count)
            {
                return false;
            }

            List<string> left = new List<string>();
            List<string> right = new List<string>();

            for (int i = 0; i < a.conditions.Count; i++)
            {
                left.Add(a.conditions[i] != null ? a.conditions[i].Describe() : "");
                right.Add(b.conditions[i] != null ? b.conditions[i].Describe() : "");
            }

            left.Sort();
            right.Sort();

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        // ===================== 条件 / 效果 =====================

        private static void WalkCondition(PuzzleCondition condition, PuzzleScanResult result,
            List<Reference> references, HashSet<string> puzzleRefs, int ruleIndex)
        {
            switch (condition)
            {
                case null:
                    return;

                case FlagCondition flag:
                    result.ReadFlags.Add(flag.flag);
                    return;

                case ObjectStateCondition state:
                    AddTargetRef(references, state.interactableId, ruleIndex, "条件的物体");
                    return;

                case HasItemCondition item:
                    result.UsedItems.Add(item.itemId);
                    AddItemRef(references, item.itemId, ruleIndex, "条件要求的道具");
                    return;

                case SelectedItemCondition item:
                    result.UsedItems.Add(item.itemId);
                    AddItemRef(references, item.itemId, ruleIndex, "条件要求的道具");
                    return;

                case PuzzleSolvedCondition solved:
                    puzzleRefs.Add(solved.puzzleId);
                    return;

                case CharacterInEraCondition character:
                    AddTargetRef(references, character.characterId, ruleIndex, "条件的人物");
                    return;

                case SelectedCharacterCondition character:
                    AddTargetRef(references, character.characterId, ruleIndex, "条件的人物");
                    return;

                case AndCondition and:
                    for (int i = 0; i < and.items.Count; i++)
                    {
                        WalkCondition(and.items[i], result, references, puzzleRefs, ruleIndex);
                    }

                    return;

                case OrCondition or:
                    for (int i = 0; i < or.items.Count; i++)
                    {
                        WalkCondition(or.items[i], result, references, puzzleRefs, ruleIndex);
                    }

                    return;

                case NotCondition not:
                    WalkCondition(not.item, result, references, puzzleRefs, ruleIndex);
                    return;
            }
        }

        private static void WalkEffect(PuzzleEffect effect, PuzzleScanResult result,
            List<Reference> references, HashSet<string> puzzleRefs, int ruleIndex)
        {
            switch (effect)
            {
                case null:
                    return;

                case SetFlagEffect flag:
                    result.WrittenFlags.Add(flag.flag);
                    return;

                case AddFlagEffect flag:
                    result.WrittenFlags.Add(flag.flag);
                    return;

                case SetObjectStateEffect state:
                    AddTargetRef(references, state.interactableId, ruleIndex, "效果的物体");
                    result.ObjectStates.Add(state.state);
                    return;

                case GiveItemEffect item:
                    result.GivenItems.Add(item.itemId);
                    return;

                case ConsumeItemEffect item:
                    result.UsedItems.Add(item.itemId);
                    AddItemRef(references, item.itemId, ruleIndex, "效果要消耗的道具");
                    return;

                case SelectItemEffect item:
                    result.UsedItems.Add(item.itemId);
                    AddItemRef(references, item.itemId, ruleIndex, "效果要选中的道具");
                    return;

                case SolvePuzzleEffect solve:
                    puzzleRefs.Add(solve.puzzleId);
                    return;

                case MoveCharacterEffect move:
                    AddTargetRef(references, move.characterId, ruleIndex, "效果搬的人物");
                    return;

                case MoveCharacterStepEffect step:
                    AddTargetRef(references, step.characterId, ruleIndex, "效果搬的人物");
                    return;

                case PlayCharacterAnimationEffect animation:
                    AddTargetRef(references, animation.characterId, ruleIndex, "效果播动画的人物");
                    return;

                case SelectCharacterEffect select:
                    AddTargetRef(references, select.characterId, ruleIndex, "效果点名的人物");
                    return;
            }
        }

        /// <summary>
        /// 登记一个「目标类」引用。`@self` 不用校验（它就是被点的那个），
        /// `@类别` 校验类别存不存在，具体 id 校验场景里有没有。
        /// </summary>
        private static void AddTargetRef(List<Reference> references, string value, int ruleIndex, string where)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            references.Add(new Reference
            {
                Value = value,
                Kind = PuzzleRef.IsSelf(value)
                    ? RefKind.Self
                    : (PuzzleRef.IsCategory(value) ? RefKind.Category : RefKind.Interaction),
                RuleIndex = ruleIndex,
                Where = where,
            });
        }

        private static void AddItemRef(List<Reference> references, string value, int ruleIndex, string where)
        {
            if (!string.IsNullOrEmpty(value))
            {
                references.Add(new Reference
                {
                    Value = value,
                    Kind = RefKind.Item,
                    RuleIndex = ruleIndex,
                    Where = where,
                });
            }
        }

        // ===================== 谜题 =====================

        private static void ScanPuzzles(PuzzleTableSO table, PuzzleScanResult result,
            List<Reference> references, HashSet<string> puzzleRefs)
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < table.puzzles.Count; i++)
            {
                PuzzleDefinition puzzle = table.puzzles[i];
                if (puzzle == null)
                {
                    result.Add(-1, true, "空谜题（列表里有个 None）。");
                    continue;
                }

                if (string.IsNullOrEmpty(puzzle.id))
                {
                    result.Add(-1, true, $"第 {i + 1} 个谜题没有 id。");
                    continue;
                }

                if (!seen.Add(puzzle.id))
                {
                    result.Add(-1, true, $"谜题 id「{puzzle.id}」重复了。");
                }

                if (puzzle.mode == PuzzleMode.Steps)
                {
                    ScanSteps(puzzle, result, references, puzzleRefs);
                }
                else if (puzzle.conditions == null || puzzle.conditions.Count == 0)
                {
                    result.Add(-1, false,
                        $"谜题「{Title(puzzle)}」没写完成条件 —— 它不会自己完成，只能被 SolvePuzzleEffect 显式完成。");
                }
                else
                {
                    for (int c = 0; c < puzzle.conditions.Count; c++)
                    {
                        WalkCondition(puzzle.conditions[c], result, references, puzzleRefs, -1);
                    }
                }

                for (int e = 0; e < puzzle.onSolved.Count; e++)
                {
                    WalkEffect(puzzle.onSolved[e], result, references, puzzleRefs, -1);
                }
            }
        }

        /// <summary>步骤式谜题：每步的条件/效果也要走一遍，不然 flag 读写统计会漏。</summary>
        private static void ScanSteps(PuzzleDefinition puzzle, PuzzleScanResult result,
            List<Reference> references, HashSet<string> puzzleRefs)
        {
            if (puzzle.steps == null || puzzle.steps.Count == 0)
            {
                result.Add(-1, false, $"谜题「{Title(puzzle)}」是步骤式但一步都没写 —— 它不会自己完成。");
                return;
            }

            HashSet<string> stepIds = new HashSet<string>();
            int requiredCount = 0;

            for (int i = 0; i < puzzle.steps.Count; i++)
            {
                PuzzleStep step = puzzle.steps[i];
                if (step == null)
                {
                    result.Add(-1, true, $"谜题「{Title(puzzle)}」第 {i + 1} 步是空的。");
                    continue;
                }

                if (string.IsNullOrEmpty(step.id))
                {
                    result.Add(-1, true, $"谜题「{Title(puzzle)}」第 {i + 1} 步没写 id（存档进度要用）。");
                }
                else if (!stepIds.Add(step.id))
                {
                    result.Add(-1, true, $"谜题「{Title(puzzle)}」有重复的步骤 id「{step.id}」。");
                }

                if (!step.optional)
                {
                    requiredCount++;
                }

                if (step.conditions == null || step.conditions.Count == 0)
                {
                    result.Add(-1, false, $"谜题「{Title(puzzle)}」第 {i + 1} 步没写条件 —— 这一步永远不会完成。");
                }
                else
                {
                    for (int c = 0; c < step.conditions.Count; c++)
                    {
                        WalkCondition(step.conditions[c], result, references, puzzleRefs, -1);
                    }
                }

                for (int e = 0; e < step.onCompleted.Count; e++)
                {
                    WalkEffect(step.onCompleted[e], result, references, puzzleRefs, -1);
                }
            }

            if (requiredCount == 0)
            {
                result.Add(-1, false, $"谜题「{Title(puzzle)}」所有步骤都是「可选」—— 它开局就会算完成。");
            }
        }

        private static void ValidatePuzzles(PuzzleTableSO table, PuzzleScanResult result, HashSet<string> puzzleRefs)
        {
            foreach (string id in puzzleRefs)
            {
                if (!string.IsNullOrEmpty(id) && table.FindPuzzle(id) == null)
                {
                    result.Add(-1, true, $"有规则/条件引用了不存在的谜题 id「{id}」。");
                }
            }
        }

        // ===================== 收尾校验 =====================

        private static void ValidateReferences(PuzzleScanResult result, List<Reference> references)
        {
            for (int i = 0; i < references.Count; i++)
            {
                Reference reference = references[i];

                switch (reference.Kind)
                {
                    case RefKind.Self:
                        continue;   // @self = 被点的那个目标，没什么可校验的

                    case RefKind.Category:
                        if (!result.Categories.Contains(PuzzleRef.CategoryName(reference.Value)))
                        {
                            result.Add(reference.RuleIndex, true,
                                $"{reference.Where}用了类别「{reference.Value}」，但场景里没有任何东西属于这个类别" +
                                "（物体的「类别」字段填了吗？人物固定属于 @character）。");
                        }

                        continue;

                    case RefKind.Item:
                        if (!result.GivenItems.Contains(reference.Value))
                        {
                            result.Add(reference.RuleIndex, true,
                                $"{reference.Where}「{reference.Value}」没有任何地方能拿到（没有任何 GiveItemEffect 给过它）。");
                        }

                        continue;

                    default:
                        if (!result.InteractionIds.Contains(reference.Value))
                        {
                            result.Add(reference.RuleIndex, true,
                                $"{reference.Where}「{reference.Value}」在场景里找不到（打错字？还是那个物体没挂 Interactable/CharacterView？）");
                        }

                        continue;
                }
            }
        }

        private static void ValidateFlags(PuzzleScanResult result)
        {
            foreach (string flag in result.ReadFlags)
            {
                if (!string.IsNullOrEmpty(flag) && !result.WrittenFlags.Contains(flag))
                {
                    result.Add(-1, false, $"flag「{flag}」只被读、没有任何地方写它 —— 条件永远不成立（拼错了？）。");
                }
            }

            foreach (string flag in result.WrittenFlags)
            {
                if (!string.IsNullOrEmpty(flag) && !result.ReadFlags.Contains(flag))
                {
                    result.Add(-1, false, $"flag「{flag}」只被写、没有任何地方读它（写了没人用）。");
                }
            }
        }

        private static string Note(InteractionRule rule) =>
            string.IsNullOrEmpty(rule.note) ? "无备注" : rule.note;

        private static string Title(PuzzleDefinition puzzle) =>
            string.IsNullOrEmpty(puzzle.title) ? puzzle.id : puzzle.title;
    }
}
