using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZGameFramework.UIFramework
{
    [Serializable]
    public class NavigationPanelEntry
    {
        [SerializeField] private Sprite  sprite=null;
        [SerializeField] private string btnText="";
        [SerializeField] private string targetScreen = "";

        public Sprite   Sprite
        {
            get => sprite;
        }
        public string BtnText
        {
            get => btnText;
        }
        public string  TargetScreen
        {
            get => targetScreen;
        }
    }

    public class NavigationPanelController:UIDemoPanelController
    {
        [SerializeField] private List<NavigationPanelEntry>  navigationPanelEntries = new List<NavigationPanelEntry>();

        [SerializeField] private NavigationPanelButton templateBtn = null;
        
        private readonly List<NavigationPanelButton> currentButtons=new List<NavigationPanelButton>();
        
        
        protected override void AddListener()
        {
            this.RegisterEvent<NavigateToWindowEvent>(OnExternalNavigation).UnregisterOnDestroyTrigger(this);
        }

        private void OnExternalNavigation(NavigateToWindowEvent obj)
        {
            foreach (var button in currentButtons)
            {
                button.SetCurrentNavigationTarget(obj.screenId);
            }
        }

        protected override void OnPropertiesSet()
        {
            ClearEntries();
            foreach (var navigationPanelEntry in navigationPanelEntries)
            {
                var newBtn = Instantiate(templateBtn);
                newBtn.transform.SetParent(templateBtn.transform.parent, false);
                newBtn.SetData(navigationPanelEntry);
                newBtn.gameObject.SetActive(true);
                newBtn.ButtonClicked += OnNavigationButtonClicked;
                currentButtons.Add(newBtn);
            }
            
            OnNavigationButtonClicked(currentButtons[0]);
        }

        private void OnNavigationButtonClicked(NavigationPanelButton currentlyClickedBtn)
        {
            this.SendEvent<NavigateToWindowEvent>(new  NavigateToWindowEvent(currentlyClickedBtn.NavigationData.TargetScreen));
            foreach (var button in currentButtons)
            {
                button.SetCurrentNavigationTarget(currentlyClickedBtn);
            }
        }

        private void ClearEntries()
        {
            foreach (var button in currentButtons)
            {
                button.ButtonClicked-= OnNavigationButtonClicked;
                Destroy(button.gameObject);
            }
            
            currentButtons.Clear();
        }
    }
}