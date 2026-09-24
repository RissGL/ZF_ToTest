using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class WindowUILayer:UILayer<IWindowController>
    {
        [SerializeField] private WindowParaLayer priorityParaLayer=null;

        public IWindowController CurrentWindow { get; private set;}

        private Queue<WindowHistoryEntry> windowQueue;
        private Stack<WindowHistoryEntry> windowHistory;
        private HashSet<IScreenController> screensTransitioning;

        public event Action RequestScreenBlock;
        public event Action RequestScreenUnblock;

        public override void Initialized()
        {
            base.Initialized();
            windowHistory=new  Stack<WindowHistoryEntry>();
            windowQueue=new Queue<WindowHistoryEntry>();
            screensTransitioning = new HashSet<IScreenController>();
        }

        protected override void ProcessRegisterScreen(string screenId, IWindowController screen)
        {
            base.ProcessRegisterScreen(screenId, screen);
            screen.InTransitionFinished += OnInAnimationFinished;
            screen.OutTransitionFinished += OutAnimationFinished;
            screen.CloseRequest += OnClosedRequest;
        }

        private void OnClosedRequest(IScreenController screen)
        {
            HideScreen(screen as IWindowController);
        }

        private void OutAnimationFinished(IScreenController screen)
        {
            RemoveTransition(screen);
            var window = screen as IWindowController;
            if (window.IsPopup)
            {
                priorityParaLayer.RefreshDarken();
            }
        }

        private void RemoveTransition(IScreenController screen)
        {
            screensTransitioning.Remove(screen);
            if (!IsScreenTransitionInProgress)
            {
                if (RequestScreenUnblock != null)
                {
                    RequestScreenUnblock();
                }
            }
        }

        public bool IsScreenTransitionInProgress
        {
            get => screensTransitioning.Count !=  0;
        }

        private void AddTransition(IScreenController screen)
        {
            screensTransitioning.Add(screen);
            if (RequestScreenBlock!=null)
            {
                RequestScreenBlock();
            }
        }

        private void OnInAnimationFinished(IScreenController screen)
        {
            RemoveTransition(screen);
        }

        protected override void ProcessUnregisterScreen(string screenControllerScreenId, IWindowController screen)
        {
            base.ProcessUnregisterScreen(screenControllerScreenId, screen);
            screen.InTransitionFinished -= OnInAnimationFinished;
            screen.OutTransitionFinished -= OutAnimationFinished;
            screen.CloseRequest -= OnClosedRequest;
        }

        public override void ShowScreen(IWindowController screen)
        {
            ShowScreen<IWindowProperties>(screen,null);
        }

        public override void ShowScreen<TProps>(IWindowController screen, TProps props)
        {
            IWindowProperties windowProps=props as  IWindowProperties;

            if (ShouldEnqueue(screen,windowProps))
            {
               EnqueueWindow(screen,windowProps); 
            }
            else
            {
                DoShow(screen,windowProps);
            }
        }

        private void DoShow(IWindowController screen, IWindowProperties windowProps)
        {
            DoShow(new WindowHistoryEntry(screen, windowProps));
        }

        private void DoShow(WindowHistoryEntry windowEntry)
        {
            if (CurrentWindow==windowEntry.Screen)
            {
                Debug.LogWarning($"{CurrentWindow.ScreenId}重复打开");
            }
            else if(CurrentWindow!=null&&CurrentWindow.HideOnForegroundLost&&!windowEntry.Screen.IsPopup)
            {
                CurrentWindow.Hide();
            }
            
            windowHistory.Push(windowEntry);
            AddTransition(windowEntry.Screen);

            if (windowEntry.Screen.IsPopup)
            {
                priorityParaLayer.DarkenBg();
            }

            windowEntry.Show();

            CurrentWindow = windowEntry.Screen;
        }

        private bool ShouldEnqueue(IWindowController screen, IWindowProperties windowProps)
        {
            if (CurrentWindow == null && windowQueue.Count == 0)
            {
                return false;
            }

            if (windowProps!=null&&windowProps.SuppressPrefabProperties)
            {
                return windowProps.WindowQueuePriority != WindowPriority.ForceForeground;
            }

            if (screen.Priority != WindowPriority.ForceForeground)
            {
                return true;
            }

            return false;
        }

        private void EnqueueWindow<TProp>(IWindowController screen, TProp windowProps) where TProp:IScreenProperties
        {
            windowQueue.Enqueue(new WindowHistoryEntry(screen, (IWindowProperties)windowProps));
        }

        public override void HideScreen(IWindowController screen)
        {
            if (screen==CurrentWindow)
            {
                windowHistory.Pop();
                AddTransition(screen);
                screen.Hide();

                CurrentWindow = null;

                if (windowQueue.Count>0)
                {
                    ShowNextInQueue();
                }
                else if (windowHistory.Count>0)
                {
                    ShowPreviousInHistory();
                }
            }
            else
            {
                Debug.LogError(string.Format( "正在非当前窗口{0},当前窗口为{1}",screen.ScreenId,CurrentWindow!=null?CurrentWindow.ScreenId:"空"));
            }
        }

        private void ShowNextInQueue()
        {
            if (windowQueue.Count>0)
            {
                var window=windowQueue.Dequeue();
                DoShow(window);
            }
        }

        private void ShowPreviousInHistory()
        {
            if (windowHistory.Count>0)
            {
                WindowHistoryEntry window=windowHistory.Pop();
                DoShow(window);
            }
        }

        public override void HideAll(bool shouldAnimateWhenHiding=true)
        {
            base.HideAll(shouldAnimateWhenHiding);
            CurrentWindow = null;
            priorityParaLayer.RefreshDarken();
            windowHistory.Clear();
            windowQueue.Clear();
        }

        public override void ReparentScreen(IScreenController controller, Transform screenTransform)
        {
            IWindowController  windowController = controller as IWindowController;

            if (windowController==null)
            {
                Debug.LogError($"{controller.ScreenId}不是winodwcontroller却在WindowUILayer下");
            }
            else if(windowController.IsPopup)
            {
                priorityParaLayer.AddScreen(screenTransform);
                return;
            }
            
            base.ReparentScreen(controller, screenTransform);
        }
    }
}