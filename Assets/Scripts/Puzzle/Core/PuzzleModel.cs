using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 谜题的**权威状态**：所有 flag、所有物体状态、背包、已解谜题。
    ///
    /// 关键设计：世界状态只有这一份，场景里的物体只是它的**视图**。
    /// 「火点着了」是这里的一个 flag，不是石器时代那个 SpriteRenderer 上的一个开关 ——
    /// 所以信息时代的壁炉能读到它，跨时代联动才成立，存档也只要存这三个字典。
    /// </summary>
    public interface IPuzzleModel : IModel
    {
        /// <summary>任何状态变了就 +1。视图靠它重算外观（比给每个物体挂一个 BindableProperty 省事）。</summary>
        IReadOnlyBindableProperty<int> Revision { get; }

        /// <summary>手里选中的道具 id，"" = 空手。</summary>
        IReadOnlyBindableProperty<string> SelectedItem { get; }

        // ---- flag：世界状态（数值 / 开关），全局唯一命名空间 ----
        float GetFlag(string flag);
        bool HasFlag(string flag);
        void SetFlag(string flag, float value);
        void SetFlag(string flag, bool value);
        void AddFlag(string flag, float delta);
        void ClearFlag(string flag);

        // ---- 物体状态：物体"现在长什么样"，默认由 SetObjectState 效果写 ----
        string GetObjectState(string interactableId);
        void SetObjectState(string interactableId, string state);

        // ---- 背包 ----
        IReadOnlyList<string> Items { get; }
        bool HasItem(string itemId);
        void AddItem(string itemId);
        bool RemoveItem(string itemId);
        void SetSelectedItem(string itemId);

        // ---- 物体/人物上写的「默认提示」----
        // 「空手点它、又没有任何规则命中时说什么」。由场景里的组件在 Awake 登记进来，
        // 是**场景内容**不是游戏状态，所以不进存档。
        string GetDefaultFeedback(string interactionId);
        void SetDefaultFeedback(string interactionId, string text);

        // ---- 场景内容登记：这个 id 是什么东西 ----
        // 规则要能按**类别**批量命中（`@rift` 管四个裂隙），还得知道"被点的这个在哪个时代"，
        // 而这两个信息只有场景里的组件自己知道。所以组件在 Awake 里把自己登记进来 ——
        // 和 defaultFeedback 同一个路子：**场景内容，不进存档**。
        //
        // 人物的时代不从这里读：人是会走的，权威在 ICharacterModel。
        void RegisterTarget(string interactionId, EraId era, string category, string displayName);
        bool IsKnownTarget(string interactionId);

        /// <summary>它属于哪个类别（没登记的返回 ""）。规则里的 `@类别` 目标和它比。</summary>
        string GetTargetCategory(string interactionId);

        /// <summary>它摆在哪个时代（没登记的返回 Stone）。规则里的「它所在的时代」用它。</summary>
        EraId GetTargetEra(string interactionId);

        /// <summary>显示名（没登记的返回 id 本身）。文案里的 {目标名} / {人物名} 用它。</summary>
        string GetTargetDisplayName(string interactionId);

        /// <summary>
        /// 挂在它身上的一句额外文案（文案里的 {目标说} 用它，比如"被点名时的一句台词"）。
        /// 通配规则（"任意人物：点名"）说不了每个人不同的话，这句就是那个出口。
        /// 和 defaultFeedback 一样是**场景内容**，不进存档。
        /// </summary>
        string GetTargetSpeech(string interactionId);

        void SetTargetSpeech(string interactionId, string text);

        // ---- 谜题 ----
        bool IsSolved(string puzzleId);
        void MarkSolved(string puzzleId);

        /// <summary>步骤式谜题：这一步做过了吗。**做过就一直算做过** —— 条件后来不成立了也不退回。</summary>
        bool IsStepDone(string puzzleId, string stepId);

        void MarkStepDone(string puzzleId, string stepId);

        // ---- 存档：状态全在这儿，所以就是一段 JSON ----
        string ToJson();
        void LoadJson(string json);
        void ResetAll();
    }

    public class PuzzleModel : AbstractModel, IPuzzleModel
    {
        private readonly BindableProperty<int> m_Revision = new BindableProperty<int>(0);
        private readonly BindableProperty<string> m_SelectedItem = new BindableProperty<string>("");

        private readonly Dictionary<string, float> m_Flags = new Dictionary<string, float>();
        private readonly Dictionary<string, string> m_ObjectStates = new Dictionary<string, string>();
        private readonly List<string> m_Items = new List<string>();
        private readonly HashSet<string> m_Solved = new HashSet<string>();

        /// <summary>步骤式谜题的进度，key = "谜题id/步骤id"。存起来，读档回来进度还在。</summary>
        private readonly HashSet<string> m_Steps = new HashSet<string>();

        /// <summary>物体/人物上写的默认提示：id → 那句话。场景内容，不进存档。</summary>
        private readonly Dictionary<string, string> m_DefaultFeedback = new Dictionary<string, string>();

        /// <summary>场景里这个 id 是什么东西（时代 / 类别 / 显示名）。场景内容，不进存档。</summary>
        private readonly Dictionary<string, TargetInfo> m_Targets = new Dictionary<string, TargetInfo>();

        /// <summary>挂在物体/人物身上的一句额外文案（{目标说}）。场景内容，不进存档。</summary>
        private readonly Dictionary<string, string> m_Speeches = new Dictionary<string, string>();

        private class TargetInfo
        {
            public EraId Era;
            public string Category;
            public string DisplayName;
        }

        public IReadOnlyBindableProperty<int> Revision => m_Revision;
        public IReadOnlyBindableProperty<string> SelectedItem => m_SelectedItem;
        public IReadOnlyList<string> Items => m_Items;

        protected override void OnInit()
        {
        }

        // ===================== flag =====================

        public float GetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return 0f;
            }

            return m_Flags.TryGetValue(flag, out float value) ? value : 0f;
        }

        public bool HasFlag(string flag) => GetFlag(flag) != 0f;

        public void SetFlag(string flag, float value)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }

            m_Flags[flag] = value;
            BumpRevision();
        }

        public void SetFlag(string flag, bool value) => SetFlag(flag, value ? 1f : 0f);

        public void AddFlag(string flag, float delta)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }

            m_Flags[flag] = GetFlag(flag) + delta;
            BumpRevision();
        }

        public void ClearFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }

            if (m_Flags.Remove(flag))
            {
                BumpRevision();
            }
        }

        // ===================== 物体状态 =====================

        public string GetObjectState(string interactableId)
        {
            if (string.IsNullOrEmpty(interactableId))
            {
                return "";
            }

            return m_ObjectStates.TryGetValue(interactableId, out string state) ? state : "";
        }

        public void SetObjectState(string interactableId, string state)
        {
            if (string.IsNullOrEmpty(interactableId))
            {
                return;
            }

            m_ObjectStates[interactableId] = state ?? "";
            BumpRevision();
        }

        // ===================== 背包 =====================

        public bool HasItem(string itemId) =>
            !string.IsNullOrEmpty(itemId) && m_Items.Contains(itemId);

        public void AddItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || m_Items.Contains(itemId))
            {
                return;
            }

            m_Items.Add(itemId);
            BumpRevision();
            this.SendEvent(new PuzzleItemEvent { ItemId = itemId, Gained = true });
        }

        public bool RemoveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !m_Items.Remove(itemId))
            {
                return false;
            }

            // 用掉的正好是手里拿着的那就自动松手
            if (PuzzleOps.SameState(m_SelectedItem.Value, itemId))
            {
                SetSelectedItem("");
            }

            BumpRevision();
            this.SendEvent(new PuzzleItemEvent { ItemId = itemId, Gained = false });
            return true;
        }

        public void SetSelectedItem(string itemId)
        {
            string value = itemId ?? "";
            if (PuzzleOps.SameState(m_SelectedItem.Value, value))
            {
                return;
            }

            m_SelectedItem.Value = value;
            this.SendEvent(new PuzzleSelectionEvent { ItemId = value });
        }

        // ===================== 默认提示 =====================

        public string GetDefaultFeedback(string interactionId) =>
            !string.IsNullOrEmpty(interactionId) && m_DefaultFeedback.TryGetValue(interactionId, out string text)
                ? text
                : "";

        public void SetDefaultFeedback(string interactionId, string text)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return;
            }

            m_DefaultFeedback[interactionId] = text ?? "";
        }

        // ===================== 场景内容登记 =====================

        public void RegisterTarget(string interactionId, EraId era, string category, string displayName)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return;
            }

            m_Targets[interactionId] = new TargetInfo
            {
                Era = era,
                Category = string.IsNullOrEmpty(category) ? PuzzleCategories.Prop : category,
                DisplayName = displayName ?? "",
            };
        }

        public bool IsKnownTarget(string interactionId) =>
            !string.IsNullOrEmpty(interactionId) && m_Targets.ContainsKey(interactionId);

        public string GetTargetCategory(string interactionId) =>
            !string.IsNullOrEmpty(interactionId) && m_Targets.TryGetValue(interactionId, out TargetInfo info)
                ? info.Category
                : "";

        public EraId GetTargetEra(string interactionId) =>
            !string.IsNullOrEmpty(interactionId) && m_Targets.TryGetValue(interactionId, out TargetInfo info)
                ? info.Era
                : EraId.Stone;

        public string GetTargetDisplayName(string interactionId)
        {
            if (!string.IsNullOrEmpty(interactionId) &&
                m_Targets.TryGetValue(interactionId, out TargetInfo info) &&
                !string.IsNullOrEmpty(info.DisplayName))
            {
                return info.DisplayName;
            }

            return interactionId ?? "";
        }

        public string GetTargetSpeech(string interactionId) =>
            !string.IsNullOrEmpty(interactionId) && m_Speeches.TryGetValue(interactionId, out string text) ? text : "";

        public void SetTargetSpeech(string interactionId, string text)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return;
            }

            m_Speeches[interactionId] = text ?? "";
        }

        // ===================== 谜题 =====================

        public bool IsSolved(string puzzleId) =>
            !string.IsNullOrEmpty(puzzleId) && m_Solved.Contains(puzzleId);

        public void MarkSolved(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId) || !m_Solved.Add(puzzleId))
            {
                return;
            }

            BumpRevision();
        }

        public bool IsStepDone(string puzzleId, string stepId) =>
            !string.IsNullOrEmpty(puzzleId) && !string.IsNullOrEmpty(stepId) &&
            m_Steps.Contains(puzzleId + "/" + stepId);

        public void MarkStepDone(string puzzleId, string stepId)
        {
            if (string.IsNullOrEmpty(puzzleId) || string.IsNullOrEmpty(stepId))
            {
                return;
            }

            if (m_Steps.Add(puzzleId + "/" + stepId))
            {
                BumpRevision();
            }
        }

        // ===================== 存档 =====================

        public void ResetAll()
        {
            m_Flags.Clear();
            m_ObjectStates.Clear();
            m_Items.Clear();
            m_Solved.Clear();
            m_Steps.Clear();
            m_SelectedItem.SetValueWithoutEvent("");
            BumpRevision();
        }

        public string ToJson()
        {
            SaveData data = new SaveData();

            foreach (KeyValuePair<string, float> pair in m_Flags)
            {
                data.flagKeys.Add(pair.Key);
                data.flagValues.Add(pair.Value);
            }

            foreach (KeyValuePair<string, string> pair in m_ObjectStates)
            {
                data.objectKeys.Add(pair.Key);
                data.objectStates.Add(pair.Value);
            }

            data.items.AddRange(m_Items);
            data.solved.AddRange(m_Solved);
            data.steps.AddRange(m_Steps);
            data.selectedItem = m_SelectedItem.Value;

            return JsonUtility.ToJson(data, true);
        }

        public void LoadJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogError("[谜题] 存档解析失败。");
                return;
            }

            m_Flags.Clear();
            m_ObjectStates.Clear();
            m_Items.Clear();
            m_Solved.Clear();

            for (int i = 0; i < data.flagKeys.Count && i < data.flagValues.Count; i++)
            {
                m_Flags[data.flagKeys[i]] = data.flagValues[i];
            }

            for (int i = 0; i < data.objectKeys.Count && i < data.objectStates.Count; i++)
            {
                m_ObjectStates[data.objectKeys[i]] = data.objectStates[i];
            }

            m_Items.AddRange(data.items);
            m_Solved.UnionWith(data.solved);

            if (data.steps != null)
            {
                m_Steps.UnionWith(data.steps);
            }

            m_SelectedItem.SetValueWithoutEvent(data.selectedItem ?? "");

            BumpRevision();
        }

        [Serializable]
        private class SaveData
        {
            public List<string> flagKeys = new List<string>();
            public List<float> flagValues = new List<float>();
            public List<string> objectKeys = new List<string>();
            public List<string> objectStates = new List<string>();
            public List<string> items = new List<string>();
            public List<string> solved = new List<string>();
            public List<string> steps = new List<string>();
            public string selectedItem = "";
        }

        private void BumpRevision() => m_Revision.Value = m_Revision.Value + 1;
    }
}
