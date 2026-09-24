using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZGameFramework.UIFramework
{
    public class UIFrame:MonoBehaviour,IController,ICanSetArchitecture
    {
        [Tooltip("如果要手动初始化设置为false")]
        [SerializeField] private bool initializeOnAwake = true;

        private PanelUILayer panelUILayer;
        private WindowUILayer windowUILayer;
        
        private Canvas mainCanvas;
        private GraphicRaycaster graphicRaycaster;
        
        private IArchitecture m_Architecture;

        public Canvas MainCanvas
        {
            get
            {
                if (mainCanvas==null)
                {
                    mainCanvas = GetComponent<Canvas>();
                }
                return mainCanvas;
            }
        }

        public Camera UICamera
        {
            get
            {
                return MainCanvas.worldCamera;
            }
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }
        

        protected virtual void Initialize()
        {
            if (panelUILayer==null)
            {
                panelUILayer = gameObject.GetComponentInChildren<PanelUILayer>(true);
                if (panelUILayer==null)
                {
                    Debug.LogError("UIFrame没有PaneLayer");
                }
                else
                {
                    panelUILayer.Initialized();
                }
            }

            if (windowUILayer==null)
            {
                windowUILayer = gameObject.GetComponentInChildren<WindowUILayer>(true);
                if (windowUILayer==null)
                {
                    Debug.LogError("UIFrame没有WindowUILayer");
                }
                else
                {
                    windowUILayer.Initialized();
                    windowUILayer.RequestScreenBlock += OnRequestScreenBlock;
                    windowUILayer.RequestScreenUnblock += OnRequestScreenUnblock;
                }
            }

            graphicRaycaster = MainCanvas.GetComponent<GraphicRaycaster>();
        }

        private void OnRequestScreenUnblock()
        {
            if (graphicRaycaster!=null)
            {
                graphicRaycaster.enabled=true;
            }
        }

        private void OnRequestScreenBlock()
        {
            if (graphicRaycaster!=null)
            {
                graphicRaycaster.enabled=false;
            }
        }

        public void ShowPanel(string panelId)
        {
            panelUILayer.ShowScreenById(panelId);
        }

        public void ShowPanel<TProps>(string panelId, TProps props)
            where TProps:IPanelProperties
        {
            panelUILayer.ShowScreenById(panelId, props);
        }
        
        public void HidePanel(string panelId)
        {
            panelUILayer.HideScreenById(panelId);
        }

        public void OpenWindow(string windowId)
        {
            windowUILayer.ShowScreenById(windowId);
        }

        public void OpenWindow<TProps>(string windowId, TProps props)
            where TProps : IWindowProperties
        {
            windowUILayer.ShowScreenById(windowId, props);
        }

        public void CloseWindow(string windowId)
        {
            windowUILayer.HideScreenById(windowId);
        }

        public void CloseCurrentWindow()
        {
            if (windowUILayer.CurrentWindow!=null)
            {
                CloseWindow(windowUILayer.CurrentWindow.ScreenId);
            }
        }

        public void ShowScreen(string id)
        {
            if (IsScreenRegistered(id,out Type type))
            {
                if (type ==typeof( IWindowController))
                {
                    OpenWindow(id);
                }

                if (type ==typeof( IPanelController))
                {
                    ShowPanel(id);
                }
            }
            else
            {
                Debug.LogError($"UI框架 {id}未注册");
            }
        }

        public void RegisterScreen(string id, IScreenController screenController, Transform screenTransform)
        {
            IWindowController  windowController = screenController as IWindowController;
            if (windowController!=null)
            {
                windowUILayer.RegisterScreen(id,windowController);
                windowController.SetArchitecture(m_Architecture);
                if (screenTransform!=null)
                {
                    windowUILayer.ReparentScreen(screenController, screenTransform);
                }
                
                return;
            }
            
            IPanelController  panelController = screenController as IPanelController;
            if (panelController != null)
            {
                panelUILayer.RegisterScreen(id, panelController);
                panelController.SetArchitecture(m_Architecture);
                if (screenTransform!=null)
                {
                    panelUILayer.ReparentScreen(screenController,screenTransform);
                }
                
                return;
            }
        }

        public void RegisterPanel<TPanel>(string screenId, TPanel panel)
            where TPanel : IPanelController
        {
            panelUILayer.RegisterScreen(screenId, panel);
        }

        public void UnregisterPanel<TPanel>(string screenId, TPanel panel) 
            where TPanel : IPanelController 
        {
            panelUILayer.UnregisterScreen(screenId, panel);
        }
        
        public void RegisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController 
        {
            windowUILayer.RegisterScreen(screenId, controller);
        }

        public void UnregisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController 
        {
            windowUILayer.UnregisterScreen(screenId, controller);
        }

        public bool IsPanelOpen(string panelId)
        {
            return panelUILayer.IsPanelVisible(panelId);
        }

        public void HideAll(bool animate = true)
        {
            HideAllPanel(animate);
            CloseAllWindow(animate);
        }

        public void CloseAllWindow(bool animate=true)
        {
            windowUILayer.HideAll(animate);
        }

        public void HideAllPanel(bool animate = true)
        {
            panelUILayer.HideAll(animate);
        }
        
        public void CloseScreen(string id)  
        {
            if (panelUILayer.IsScreenRegistered(id))
            {
                HidePanel(id);
            }
            else if (windowUILayer.IsScreenRegistered(id))
            {
                CloseWindow(id);
            }
        }
        
        public bool IsScreenRegistered(string screenId)
        {
            if (windowUILayer.IsScreenRegistered(screenId))
            {
                return true;
            }

            if (panelUILayer.IsScreenRegistered(screenId))
            {
                return true;
            }

            return false;
        }
        
        public bool IsScreenRegistered(string screenId,out  Type screenType)
        {
            if (windowUILayer.IsScreenRegistered(screenId))
            {
                screenType = typeof(IWindowController);
                return true;
            }

            if (panelUILayer.IsScreenRegistered(screenId))
            {
                screenType = typeof(IPanelController);
                return true;
            }

            screenType = null;
            return false;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture() => m_Architecture;
        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            m_Architecture = architecture;
        }
    }
}