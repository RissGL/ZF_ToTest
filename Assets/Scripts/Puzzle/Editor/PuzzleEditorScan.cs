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
        private struct Reference
        {
            public string Value;
            public bool MustBeInteraction;   // true = 必须是场景里的东西；false = 必须是某个道具
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
                }

                if (views[i] is CharacterView character)
                {
                    result.CharacterIds.Add(character.CharacterId);
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
                    references.Add(new Reference
                    {
                        Value = rule.targetId,
                        MustBeInteraction = true,
                        RuleIndex = i,
                        Where = "规则的目标",
                    });
                }
                else if (rule.verb == Verb.UseItem)
                {
                    result.Add(i, true, "动作是「用道具」但目标留空了 —— 通配目标 + 指定道具会抢走太多点击。");
                }

                if (!string.IsNullOrEmpty(rule.itemId))
                {
                    references.Add(new Reference
                    {
                        Value = rule.itemId,
                        MustBeInteraction = false,
                        RuleIndex = i,
                        Where = "规则要求用的道具",
                    });
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
                if (rule == null || rule.conditions.Count != 0)
                {
                    continue;
                }

                bool onlyFeedback = rule.effects.Count == 1 && rule.effects[0] is FeedbackEffect;
                if (!onlyFeedback)
                {
                    continue;
                }

                // 上面有没有同类（同目标同动作）的规则？有的话它就是纯兜底，直接并上去
                int sameTargetAbove = -1;
                for (int j = 0; j < i; j++)
                {
                    if (Covers(rules[j], rule) || Covers(rule, rules[j]))
                    {
                        sameTargetAbove = j;
                        break;
                    }
                }

                result.Add(i, false, sameTargetAbove >= 0
                    ? $"这条只是兜底提示，不用单拉一条 —— 把这句话填到第 {sameTargetAbove + 1} 条的「条件不满足时，对玩家说」里就行。"
                    : "这条只是「点它没反应时说一句话」—— 建议改成填在物体自己的「空手点它时的默认提示」上，规则表里就少一行。");
            }
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
                    if (earlier == null || !Covers(earlier, later))
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

        /// <summary>earlier 是否"覆盖"了 later：earlier 能匹配到 later 能匹配的所有点击。</summary>
        private static bool Covers(InteractionRule earlier, InteractionRule later)
        {
            if (!string.IsNullOrEmpty(earlier.targetId) && earlier.targetId != later.targetId)
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
                    AddInteractionRef(references, state.interactableId, ruleIndex, "条件的物体");
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
                    references.Add(new Reference
                    {
                        Value = character.characterId,
                        MustBeInteraction = true,
                        RuleIndex = ruleIndex,
                        Where = "条件的人物",
                    });
                    return;

                case SelectedCharacterCondition character:
                    if (!string.IsNullOrEmpty(character.characterId))
                    {
                        references.Add(new Reference
                        {
                            Value = character.characterId,
                            MustBeInteraction = true,
                            RuleIndex = ruleIndex,
                            Where = "条件的人物",
                        });
                    }

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
                    AddInteractionRef(references, state.interactableId, ruleIndex, "效果的物体");
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
                    references.Add(new Reference
                    {
                        Value = move.characterId,
                        MustBeInteraction = true,
                        RuleIndex = ruleIndex,
                        Where = "效果搬的人物",
                    });
                    return;

                case SelectCharacterEffect select:
                    if (!string.IsNullOrEmpty(select.characterId))
                    {
                        references.Add(new Reference
                        {
                            Value = select.characterId,
                            MustBeInteraction = true,
                            RuleIndex = ruleIndex,
                            Where = "效果点名的人物",
                        });
                    }

                    return;
            }
        }

        private static void AddInteractionRef(List<Reference> references, string value, int ruleIndex, string where)
        {
            if (!string.IsNullOrEmpty(value))
            {
                references.Add(new Reference
                {
                    Value = value,
                    MustBeInteraction = true,
                    RuleIndex = ruleIndex,
                    Where = where,
                });
            }
        }

        private static void AddItemRef(List<Reference> references, string value, int ruleIndex, string where)
        {
            if (!string.IsNullOrEmpty(value))
            {
                references.Add(new Reference
                {
                    Value = value,
                    MustBeInteraction = false,
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
                bool known = reference.MustBeInteraction
                    ? result.InteractionIds.Contains(reference.Value)
                    : result.GivenItems.Contains(reference.Value);

                if (!known)
                {
                    result.Add(reference.RuleIndex, true, reference.MustBeInteraction
                        ? $"{reference.Where}「{reference.Value}」在场景里找不到（打错字？还是那个物体没挂 Interactable/CharacterView？）"
                        : $"{reference.Where}「{reference.Value}」没有任何地方能拿到（没有任何 GiveItemEffect 给过它）。");
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
