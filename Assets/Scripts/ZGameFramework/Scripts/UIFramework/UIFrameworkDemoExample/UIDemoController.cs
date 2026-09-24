using System;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class UIDemoController:MonoBehaviour,IController
    {
        [SerializeField] private UISetting defaultUISetting;
        [SerializeField] private Camera uiCam = null;
        [SerializeField] private Transform transformToFollow = null;

        [SerializeField] private FakePlayerData fakePlayerData = null;
        
        private UIFrame uiFrame;

        private void Awake()
        {
            uiFrame = defaultUISetting.CreateUIInstance(GetArchitecture());
            this.RegisterEvent<StartUIDemoEvent>(OnStartDemo).UnregisterOnDestroyTrigger(this);
            this.RegisterEvent<NavigateToWindowEvent>(OnNavigateToWindow).UnregisterOnDestroyTrigger(this);
            this.RegisterEvent<ShowConfirmationPopupEvent>(OnShowConfirmationPopup).UnregisterOnDestroyTrigger(this);
            
        }

        private void Start()
        {
            uiFrame.OpenWindow(ExampleScreenIds.START_GAME_WINDOW);
        }

        private void OnStartDemo()
        {
            uiFrame.ShowPanel(ExampleScreenIds.NAVIGATION_PANEL);
            uiFrame.ShowPanel(ExampleScreenIds.ToastPanel);
        } 

        private void OnNavigateToWindow(NavigateToWindowEvent navigateToWindowEvent)
        {
            uiFrame.CloseCurrentWindow();
            
            switch (navigateToWindowEvent.screenId) {
                case ExampleScreenIds.PlayerWindow:
                    uiFrame.OpenWindow(navigateToWindowEvent.screenId, new PlayerWindowProperties(fakePlayerData.LevelProgress));
                    break;
                case ExampleScreenIds.CameraProjectionWindow:
                    transformToFollow.parent.gameObject.SetActive(true);
                    uiFrame.OpenWindow(navigateToWindowEvent.screenId, new CameraProjectionWindowProperties(uiCam, transformToFollow));
                    break;
                default:
                    uiFrame.OpenWindow(navigateToWindowEvent.screenId);
                    break;
            }
        }

        private void OnShowConfirmationPopup(ShowConfirmationPopupEvent popupPayload) 
        {
            Debug.LogWarning("open"+ExampleScreenIds.PopupExampleWindow);
            uiFrame.OpenWindow(ExampleScreenIds.ConfirmationPopup, popupPayload.properties);
        }
        
        public IArchitecture GetArchitecture()
        {
            return UIExampleDemo.Interface;
        }
    }
}