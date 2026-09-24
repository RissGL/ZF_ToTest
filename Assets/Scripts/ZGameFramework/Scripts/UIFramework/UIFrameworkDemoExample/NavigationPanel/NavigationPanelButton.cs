using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace ZGameFramework.UIFramework
{
    [RequireComponent(typeof(Button))]
    public class NavigationPanelButton:MonoBehaviour
    {
        private Button button;

        public Button _Button
        {
            get
            {
                if (button==null)
                {
                    button = GetComponent<Button>();
                }
                
                return button;
            }
        }

        [SerializeField] private Image btnIcon = null;
        [SerializeField] private Text  btnText = null;
        private NavigationPanelEntry navigationData=null;

        public NavigationPanelEntry NavigationData
        {
            get => navigationData;
        }


        public event Action<NavigationPanelButton> ButtonClicked;

        public string Target
        {
            get=>navigationData.TargetScreen;
        }

        private void Awake()
        {
            _Button.onClick.AddListener(UI_Click);
        }

        public void SetData(NavigationPanelEntry navigationPanelEntry)
        {
            btnText.text=navigationPanelEntry.BtnText;
            btnIcon.sprite=navigationPanelEntry.Sprite;
            navigationData=navigationPanelEntry;
        }

        public void SetCurrentNavigationTarget(NavigationPanelButton selectedButton)
        {
            button.interactable=selectedButton!=this;
        }
        public void SetCurrentNavigationTarget(string screenId)
        {
            if (navigationData!=null)
            {
                _Button.interactable = navigationData.TargetScreen == screenId;
            }
        }
        
        public void UI_Click()
        {
            ButtonClicked?.Invoke(this);
        }
    }
}