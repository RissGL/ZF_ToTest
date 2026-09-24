using System;

namespace ZGameFramework.UIFramework
{
    public interface IScreenController:IController,ICanSetArchitecture
    {
        string ScreenId { get; set; }
        bool IsVisible { get;  }
        
        void Show(IScreenProperties props =null);
        void Hide(bool animate = true);
        
        Action<IScreenController> InTransitionFinished { get; set; }

        Action<IScreenController>OutTransitionFinished { get; set; }
        Action<IScreenController> CloseRequest { get; set; }

        Action<IScreenController> ScreenDestroyed { get; set; }
    }

    public interface IPanelController:IScreenController
    {
        PanelPriority Priority { get;  }
    }

    public interface IWindowController : IScreenController
    {
        bool HideOnForegroundLost { get; }
        bool IsPopup { get; }
        WindowPriority Priority { get; }
    }
}