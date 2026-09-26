using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 人物的**权威状态**：一个人现在在哪个时代，以及他开局在哪个时代。
    ///
    /// 和谜题状态一样，这里只有数据 —— 场景里的角色 GameObject 只是视图。
    /// 「阿岩现在在蒸汽时代」是这里的一条记录，不是他挂在哪个 Transform 下面。
    /// 这样他才能真正跨窗口搬，存档也才存得下来。
    /// </summary>
    public interface ICharacterModel : IModel
    {
        /// <summary>任何变动（有人换时代、有人登记进来）就 +1。视图靠它重算。</summary>
        IReadOnlyBindableProperty<int> Revision { get; }

        /// <summary>场上所有人物 id（顺序 = 登记顺序）。</summary>
        IReadOnlyList<string> Characters { get; }

        bool IsKnown(string characterId);

        /// <summary>他现在在哪个时代。不认识的人返回 homeEra 的兜底（Stone）。</summary>
        EraId GetEra(string characterId);

        /// <summary>他开局在哪个时代（复位用）。</summary>
        EraId GetHomeEra(string characterId);

        /// <summary>场景里的 CharacterView 在 Awake 里登记自己。同一个人重复登记不会覆盖他当前所在的时代。</summary>
        void Register(string characterId, EraId homeEra);

        void SetEra(string characterId, EraId era);

        /// <summary>当前**点名**的人物 id，"" = 没点任何人。点名的那个就是「单独送走」的对象。</summary>
        IReadOnlyBindableProperty<string> SelectedCharacter { get; }

        /// <summary>点名 / 取消点名（传 "" 或 null 取消）。</summary>
        void SetSelectedCharacter(string characterId);

        /// <summary>这个时代里有几个人。</summary>
        int CountInEra(EraId era);

        /// <summary>这个时代里都有谁（会新建一个 List）。</summary>
        List<string> InEra(EraId era);

        string ToJson();
        void LoadJson(string json);

        /// <summary>所有人回到自己开局的年代。</summary>
        void ResetAll();
    }

    public class CharacterModel : AbstractModel, ICharacterModel
    {
        private readonly BindableProperty<int> m_Revision = new BindableProperty<int>(0);
        private readonly List<string> m_Order = new List<string>();
        private readonly Dictionary<string, EraId> m_Current = new Dictionary<string, EraId>();
        private readonly Dictionary<string, EraId> m_Home = new Dictionary<string, EraId>();
        private readonly BindableProperty<string> m_SelectedCharacter = new BindableProperty<string>("");

        public IReadOnlyBindableProperty<int> Revision => m_Revision;
        public IReadOnlyList<string> Characters => m_Order;
        public IReadOnlyBindableProperty<string> SelectedCharacter => m_SelectedCharacter;

        protected override void OnInit()
        {
        }

        public bool IsKnown(string characterId) =>
            !string.IsNullOrEmpty(characterId) && m_Current.ContainsKey(characterId);

        public EraId GetEra(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && m_Current.TryGetValue(characterId, out EraId era))
            {
                return era;
            }

            return EraId.Stone;
        }

        public EraId GetHomeEra(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && m_Home.TryGetValue(characterId, out EraId era))
            {
                return era;
            }

            return EraId.Stone;
        }

        public void Register(string characterId, EraId homeEra)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return;
            }

            bool changed = false;

            if (!m_Home.ContainsKey(characterId))
            {
                m_Home[characterId] = homeEra;
                changed = true;
            }

            if (!m_Current.ContainsKey(characterId))
            {
                // 已经在场上的（读档回来的）不覆盖当前位置
                m_Current[characterId] = homeEra;
                changed = true;
            }

            if (!m_Order.Contains(characterId))
            {
                m_Order.Add(characterId);
                changed = true;
            }

            if (changed)
            {
                BumpRevision();
            }
        }

        public void SetEra(string characterId, EraId era)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return;
            }

            if (m_Current.TryGetValue(characterId, out EraId current) && current == era)
            {
                return;
            }

            m_Current[characterId] = era;

            if (!m_Order.Contains(characterId))
            {
                m_Order.Add(characterId);
            }

            BumpRevision();
        }

        public void SetSelectedCharacter(string characterId)
        {
            string value = characterId ?? "";

            if (PuzzleOps.SameState(m_SelectedCharacter.Value, value))
            {
                return;
            }

            m_SelectedCharacter.Value = value;
            BumpRevision();
        }

        public int CountInEra(EraId era)
        {
            int count = 0;
            for (int i = 0; i < m_Order.Count; i++)
            {
                if (GetEra(m_Order[i]) == era)
                {
                    count++;
                }
            }

            return count;
        }

        public List<string> InEra(EraId era)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < m_Order.Count; i++)
            {
                if (GetEra(m_Order[i]) == era)
                {
                    result.Add(m_Order[i]);
                }
            }

            return result;
        }

        public void ResetAll()
        {
            for (int i = 0; i < m_Order.Count; i++)
            {
                string id = m_Order[i];
                m_Current[id] = GetHomeEra(id);
            }

            m_SelectedCharacter.SetValueWithoutEvent("");
            BumpRevision();
        }

        // ===================== 存档 =====================

        public string ToJson()
        {
            SaveData data = new SaveData();

            for (int i = 0; i < m_Order.Count; i++)
            {
                string id = m_Order[i];
                data.ids.Add(id);
                data.current.Add((int)GetEra(id));
                data.home.Add((int)GetHomeEra(id));
            }

            data.selectedCharacter = m_SelectedCharacter.Value;
            return JsonUtility.ToJson(data, true);
        }

        public void LoadJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.ids == null)
            {
                Debug.LogError("[人物] 存档解析失败。");
                return;
            }

            m_Order.Clear();
            m_Current.Clear();
            m_Home.Clear();

            int count = data.ids.Count;
            for (int i = 0; i < count; i++)
            {
                string id = data.ids[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                m_Order.Add(id);
                m_Current[id] = i < data.current.Count ? (EraId)data.current[i] : EraId.Stone;
                m_Home[id] = i < data.home.Count ? (EraId)data.home[i] : m_Current[id];
            }

            m_SelectedCharacter.SetValueWithoutEvent(data.selectedCharacter ?? "");
            BumpRevision();
        }

        [Serializable]
        private class SaveData
        {
            public List<string> ids = new List<string>();
            public List<int> current = new List<int>();
            public List<int> home = new List<int>();
            public string selectedCharacter = "";
        }

        private void BumpRevision() => m_Revision.Value = m_Revision.Value + 1;
    }
}
