using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZF.EraGallery;

namespace ZF.Puzzle.EditorTools
{
    /// <summary>
    /// 编辑器里的「试跑」：不启动游戏，把一个假的当前状态摆出来，
    /// 然后拿**真的规则表**、走**真的匹配逻辑**（PuzzleRuleMatcher）跑一遍。
    ///
    /// 价值不在"告诉你命中了哪条"，而在"告诉你每条为什么没中" —— 条件断在哪一条上一眼就看见。
    /// </summary>
    public class PuzzleSimulation
    {
        public readonly SimPuzzleModel Puzzle = new SimPuzzleModel();
        public readonly SimCharacterModel Characters = new SimCharacterModel();
        public readonly SimCharacterSystem CharacterOps = new SimCharacterSystem();

        /// <summary>模拟"玩家现在进在第几个时代里"（-1 = 全景）。</summary>
        public int FocusedEraIndex = -1;

        public PuzzleSimulation()
        {
            CharacterOps.Bind(Characters);
        }

        /// <summary>把编辑器面板上摆的值灌进假状态。</summary>
        public void Apply(IEnumerable<KeyValuePair<string, float>> flags,
            IEnumerable<string> items, string selectedItem,
            IEnumerable<KeyValuePair<string, EraId>> characterEras, string selectedCharacter)
        {
            Puzzle.ResetAll();
            Characters.ResetAll();

            if (flags != null)
            {
                foreach (KeyValuePair<string, float> pair in flags)
                {
                    Puzzle.SetFlag(pair.Key, pair.Value);
                }
            }

            if (items != null)
            {
                foreach (string item in items)
                {
                    Puzzle.AddItem(item);
                }
            }

            Puzzle.SetSelectedItem(selectedItem ?? "");

            if (characterEras != null)
            {
                foreach (KeyValuePair<string, EraId> pair in characterEras)
                {
                    Characters.Register(pair.Key, pair.Value);
                    Characters.SetEra(pair.Key, pair.Value);
                }
            }

            Characters.SetSelectedCharacter(selectedCharacter ?? "");
            CharacterOps.Log.Clear();
        }

        /// <summary>跑一次「玩家点了 targetId」。返回一行行可读的过程。</summary>
        public List<string> Trace(PuzzleTableSO table, string targetId, Verb verb, string itemId)
        {
            List<string> lines = new List<string>();

            if (table == null)
            {
                lines.Add("没有规则表。");
                return lines;
            }

            PuzzleContext context = new PuzzleContext
            {
                TargetId = targetId ?? "",
                Verb = verb,
                UsedItemId = itemId ?? "",
                State = Puzzle,
                Characters = Characters,
                CharacterOps = CharacterOps,
                FocusedEraIndex = FocusedEraIndex,
                Feedback = message => lines.Add("   ▸ 提示：" + message),
                SolvePuzzle = id =>
                {
                    Puzzle.MarkSolved(id);
                    lines.Add($"   ▸ 谜题[{id}] 完成");
                },
            };

            List<InteractionRule> rules = table.interactionRules;

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (!PuzzleRuleMatcher.MatchesTarget(rule, context.TargetId, verb, itemId))
                {
                    continue;   // 目标和动作都不对，不啰嗦
                }

                context.Rule = rule;

                List<string> failed = new List<string>();
                for (int c = 0; c < rule.conditions.Count; c++)
                {
                    PuzzleCondition condition = rule.conditions[c];
                    if (condition != null && !condition.IsMet(context))
                    {
                        failed.Add(condition.Describe());
                    }
                }

                if (failed.Count > 0)
                {
                    lines.Add($"#{i + 1} ✗ 条件不满足：{string.Join("、", failed)}");
                    if (!string.IsNullOrEmpty(rule.elseFeedback))
                    {
                        lines.Add($"     它的提示会被用上：「{rule.elseFeedback}」");
                    }

                    continue;
                }

                lines.Add($"#{i + 1} ✅ 命中（第一条全通过的就是它）");
                lines.Add(rule.conditions.Count == 0
                    ? "     条件：（无条件）"
                    : $"     条件：{rule.conditions.Count} 条全通过");

                for (int e = 0; e < rule.effects.Count; e++)
                {
                    PuzzleEffect effect = rule.effects[e];
                    if (effect == null)
                    {
                        continue;
                    }

                    lines.Add("     效果：" + effect.Describe());
                    effect.Execute(context);
                }

                AppendCharacterLog(lines);
                return lines;
            }

            lines.Add("没有任何规则命中（点了没反应）。");
            AppendCharacterLog(lines);
            return lines;
        }

        private void AppendCharacterLog(List<string> lines)
        {
            for (int i = 0; i < CharacterOps.Log.Count; i++)
            {
                lines.Add("   ▸ " + CharacterOps.Log[i]);
            }

            CharacterOps.Log.Clear();
        }

        // ===================== 假 Model / System =====================

        public class SimPuzzleModel : IPuzzleModel
        {
            private readonly BindableProperty<int> m_Revision = new BindableProperty<int>(0);
            private readonly BindableProperty<string> m_SelectedItem = new BindableProperty<string>("");
            private readonly Dictionary<string, float> m_Flags = new Dictionary<string, float>();
            private readonly Dictionary<string, string> m_ObjectStates = new Dictionary<string, string>();
            private readonly List<string> m_Items = new List<string>();
            private readonly HashSet<string> m_Solved = new HashSet<string>();
            private readonly HashSet<string> m_Steps = new HashSet<string>();
            private readonly Dictionary<string, string> m_DefaultFeedback = new Dictionary<string, string>();

            public IReadOnlyBindableProperty<int> Revision => m_Revision;
            public IReadOnlyBindableProperty<string> SelectedItem => m_SelectedItem;
            public IReadOnlyList<string> Items => m_Items;

            public bool Initialized { get; set; }
            public void Init() { }
            public void DeInit() { }
            public IArchitecture GetArchitecture() => null;
            public void SetArchitecture(IArchitecture architecture) { }

            public float GetFlag(string flag) =>
                !string.IsNullOrEmpty(flag) && m_Flags.TryGetValue(flag, out float value) ? value : 0f;

            public bool HasFlag(string flag) => GetFlag(flag) != 0f;

            public void SetFlag(string flag, float value)
            {
                if (!string.IsNullOrEmpty(flag)) { m_Flags[flag] = value; Bump(); }
            }

            public void SetFlag(string flag, bool value) => SetFlag(flag, value ? 1f : 0f);

            public void AddFlag(string flag, float delta)
            {
                if (!string.IsNullOrEmpty(flag)) { m_Flags[flag] = GetFlag(flag) + delta; Bump(); }
            }

            public void ClearFlag(string flag)
            {
                if (!string.IsNullOrEmpty(flag) && m_Flags.Remove(flag)) { Bump(); }
            }

            public string GetObjectState(string interactableId) =>
                !string.IsNullOrEmpty(interactableId) && m_ObjectStates.TryGetValue(interactableId, out string state) ? state : "";

            public void SetObjectState(string interactableId, string state)
            {
                if (!string.IsNullOrEmpty(interactableId)) { m_ObjectStates[interactableId] = state ?? ""; Bump(); }
            }

            public bool HasItem(string itemId) => !string.IsNullOrEmpty(itemId) && m_Items.Contains(itemId);

            public void AddItem(string itemId)
            {
                if (!string.IsNullOrEmpty(itemId) && !m_Items.Contains(itemId)) { m_Items.Add(itemId); Bump(); }
            }

            public bool RemoveItem(string itemId)
            {
                if (string.IsNullOrEmpty(itemId) || !m_Items.Remove(itemId)) { return false; }
                if (PuzzleOps.SameState(m_SelectedItem.Value, itemId)) { m_SelectedItem.SetValueWithoutEvent(""); }
                Bump();
                return true;
            }

            public void SetSelectedItem(string itemId) => m_SelectedItem.Value = itemId ?? "";

            public bool IsSolved(string puzzleId) => !string.IsNullOrEmpty(puzzleId) && m_Solved.Contains(puzzleId);

            public void MarkSolved(string puzzleId)
            {
                if (!string.IsNullOrEmpty(puzzleId) && m_Solved.Add(puzzleId)) { Bump(); }
            }

            public bool IsStepDone(string puzzleId, string stepId) =>
                !string.IsNullOrEmpty(puzzleId) && !string.IsNullOrEmpty(stepId) &&
                m_Steps.Contains(puzzleId + "/" + stepId);

            public void MarkStepDone(string puzzleId, string stepId)
            {
                if (!string.IsNullOrEmpty(puzzleId) && !string.IsNullOrEmpty(stepId) &&
                    m_Steps.Add(puzzleId + "/" + stepId))
                {
                    Bump();
                }
            }

            public string GetDefaultFeedback(string interactionId)
            {
                if (string.IsNullOrEmpty(interactionId))
                {
                    return "";
                }

                return m_DefaultFeedback.TryGetValue(interactionId, out string text) ? text : "";
            }

            public void SetDefaultFeedback(string interactionId, string text)
            {
                if (!string.IsNullOrEmpty(interactionId))
                {
                    m_DefaultFeedback[interactionId] = text ?? "";
                }
            }

            public string ToJson() => "";
            public void LoadJson(string json) { }

            public void ResetAll()
            {
                m_Flags.Clear();
                m_ObjectStates.Clear();
                m_Items.Clear();
                m_Solved.Clear();
                m_Steps.Clear();
                m_SelectedItem.SetValueWithoutEvent("");
                Bump();
            }

            private void Bump() => m_Revision.Value = m_Revision.Value + 1;
        }

        public class SimCharacterModel : ICharacterModel
        {
            private readonly BindableProperty<int> m_Revision = new BindableProperty<int>(0);
            private readonly BindableProperty<string> m_Selected = new BindableProperty<string>("");
            private readonly List<string> m_Order = new List<string>();
            private readonly Dictionary<string, EraId> m_Current = new Dictionary<string, EraId>();
            private readonly Dictionary<string, EraId> m_Home = new Dictionary<string, EraId>();

            public IReadOnlyBindableProperty<int> Revision => m_Revision;
            public IReadOnlyList<string> Characters => m_Order;
            public IReadOnlyBindableProperty<string> SelectedCharacter => m_Selected;

            public bool Initialized { get; set; }
            public void Init() { }
            public void DeInit() { }
            public IArchitecture GetArchitecture() => null;
            public void SetArchitecture(IArchitecture architecture) { }

            public bool IsKnown(string characterId) => !string.IsNullOrEmpty(characterId) && m_Current.ContainsKey(characterId);

            public EraId GetEra(string characterId) =>
                !string.IsNullOrEmpty(characterId) && m_Current.TryGetValue(characterId, out EraId era) ? era : EraId.Stone;

            public EraId GetHomeEra(string characterId) =>
                !string.IsNullOrEmpty(characterId) && m_Home.TryGetValue(characterId, out EraId era) ? era : EraId.Stone;

            public void Register(string characterId, EraId homeEra)
            {
                if (string.IsNullOrEmpty(characterId)) { return; }

                if (!m_Home.ContainsKey(characterId)) { m_Home[characterId] = homeEra; }
                if (!m_Current.ContainsKey(characterId)) { m_Current[characterId] = homeEra; }
                if (!m_Order.Contains(characterId)) { m_Order.Add(characterId); }
                Bump();
            }

            public void SetEra(string characterId, EraId era)
            {
                if (string.IsNullOrEmpty(characterId) || GetEra(characterId) == era) { return; }
                m_Current[characterId] = era;
                if (!m_Order.Contains(characterId)) { m_Order.Add(characterId); }
                Bump();
            }

            public void SetSelectedCharacter(string characterId) => m_Selected.Value = characterId ?? "";

            public int CountInEra(EraId era)
            {
                int count = 0;
                for (int i = 0; i < m_Order.Count; i++)
                {
                    if (GetEra(m_Order[i]) == era) { count++; }
                }

                return count;
            }

            public List<string> InEra(EraId era)
            {
                List<string> result = new List<string>();
                for (int i = 0; i < m_Order.Count; i++)
                {
                    if (GetEra(m_Order[i]) == era) { result.Add(m_Order[i]); }
                }

                return result;
            }

            public string ToJson() => "";
            public void LoadJson(string json) { }

            public void ResetAll()
            {
                for (int i = 0; i < m_Order.Count; i++)
                {
                    m_Current[m_Order[i]] = GetHomeEra(m_Order[i]);
                }

                m_Selected.SetValueWithoutEvent("");
                Bump();
            }

            private void Bump() => m_Revision.Value = m_Revision.Value + 1;
        }

        public class SimCharacterSystem : ICharacterSystem
        {
            public readonly List<string> Log = new List<string>();

            private SimCharacterModel m_Model;

            public bool Initialized { get; set; }
            public void Init() { }
            public void DeInit() { }
            public IArchitecture GetArchitecture() => null;
            public void SetArchitecture(IArchitecture architecture) { }

            public void Bind(SimCharacterModel model) => m_Model = model;

            public bool TryMove(string characterId, EraId targetEra)
            {
                if (m_Model == null || !m_Model.IsKnown(characterId)) { return false; }

                EraId from = m_Model.GetEra(characterId);
                if (from == targetEra) { return false; }

                m_Model.SetEra(characterId, targetEra);
                Log.Add($"[{characterId}] 从 {from} 搬到 {targetEra}");
                return true;
            }

            public bool PlayAnimation(string characterId, string clip)
            {
                if (m_Model == null) { return false; }

                string target = string.IsNullOrEmpty(characterId) ? m_Model.SelectedCharacter.Value : characterId;
                if (string.IsNullOrEmpty(target)) { return false; }

                Log.Add($"[{target}] 播动画「{clip}」");
                return true;
            }

            public int TryMoveEra(EraId fromEra, EraId targetEra)
            {
                if (m_Model == null || fromEra == targetEra) { return 0; }

                List<string> moving = m_Model.InEra(fromEra);
                int moved = 0;
                for (int i = 0; i < moving.Count; i++)
                {
                    if (TryMove(moving[i], targetEra)) { moved++; }
                }

                return moved;
            }

            public bool Select(string characterId)
            {
                if (m_Model == null) { return false; }

                string value = characterId ?? "";
                if (!string.IsNullOrEmpty(value) && !m_Model.IsKnown(value)) { return false; }

                m_Model.SetSelectedCharacter(value);
                Log.Add(string.IsNullOrEmpty(value) ? "取消点名" : $"点名 [{value}]");
                return true;
            }

            public void ResetAll() => m_Model?.ResetAll();
        }
    }
}
