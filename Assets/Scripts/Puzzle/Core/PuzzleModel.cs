using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;

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

        // ---- 谜题 ----
        bool IsSolved(string puzzleId);
        void MarkSolved(string puzzleId);

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

        // ===================== 存档 =====================

        public void ResetAll()
        {
            m_Flags.Clear();
            m_ObjectStates.Clear();
            m_Items.Clear();
            m_Solved.Clear();
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
            public string selectedItem = "";
        }

        private void BumpRevision() => m_Revision.Value = m_Revision.Value + 1;
    }
}
