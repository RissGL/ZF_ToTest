using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    [Serializable]
    public class PlayerWindowProperties : WindowProperties
    {
        public readonly List<PlayerDataEntry> PlayerData;

        public PlayerWindowProperties(List<PlayerDataEntry> data) {
            PlayerData = data;
        }
    }
    
    public class PlayerWindowController:UIDemoWindowController<PlayerWindowProperties>
    {
        [SerializeField] 
        private LevelProgressComponent templateLevelEntry = null;
        
        private List<LevelProgressComponent> currentLevels = new List<LevelProgressComponent>();

        protected override void AddListener()
        {
            this.RegisterEvent<PlayerDataUpdateEvent>(OnDataUpdated).UnregisterOnDestroyTrigger(this);
        }

        protected override void OnPropertiesSet()
        {
            OnDataUpdated(Properties.PlayerData);
        }

        private void OnDataUpdated(List<PlayerDataEntry> data) {
            VerifyElementCount(data.Count);
            RefreshElementData(data);
        }
        
        private void OnDataUpdated(PlayerDataUpdateEvent updateEvent) 
        {
            VerifyElementCount(updateEvent.playerLevelProgress.Count);
            RefreshElementData(updateEvent.playerLevelProgress);
        }

        private void RefreshElementData(List<PlayerDataEntry> data)
        {
            for (int i = 0; i < currentLevels.Count; i++) 
            {
                currentLevels[i].SetData(data[i], i);
            }
        }

        private void VerifyElementCount(int levelCount)
        {
            if (currentLevels.Count == levelCount) 
            {
                return;
            }

            if (currentLevels.Count < levelCount) 
            {
                while (currentLevels.Count < levelCount) 
                {
                    var newLevel = Instantiate(templateLevelEntry, 
                        templateLevelEntry.transform.parent, 
                        false);
                    newLevel.gameObject.SetActive(true);
                    currentLevels.Add(newLevel);
                }
            }
            else 
            {
                while (currentLevels.Count > levelCount) 
                {
                    var levelToRemove = currentLevels[currentLevels.Count - 1];
                    currentLevels.Remove(levelToRemove);
                    Destroy(levelToRemove.gameObject);
                }
            }
        }
    }
}