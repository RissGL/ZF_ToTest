using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Core;

namespace ZGameFramework.UIFramework
{

    public class PlayerDataUpdateEvent : GameEvent
    {
        public readonly List<PlayerDataEntry> playerLevelProgress = null;

        public PlayerDataUpdateEvent(List<PlayerDataEntry> playerLevelProgress)
        {
            this.playerLevelProgress = playerLevelProgress;
        }
    }

    [Serializable]
    public class PlayerDataEntry
    {
        public string LevelName;
        [Range(0,3)]
        public int Stars;
    }

    

    [CreateAssetMenu(fileName = "PlayerData", menuName = "ZGameFramework/UI/Fake Player Data")]
    public class FakePlayerData : ScriptableObject
    {
        [SerializeField]
        private List<PlayerDataEntry> levelProgress = null;

        public List<PlayerDataEntry> LevelProgress {
            get { return levelProgress; }
        }

        private void OnValidate()
        {
            EventBus.Publish<PlayerDataUpdateEvent>(new PlayerDataUpdateEvent(levelProgress));
        }
    }
}