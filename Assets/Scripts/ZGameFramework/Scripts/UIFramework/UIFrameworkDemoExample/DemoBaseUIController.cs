namespace ZGameFramework.UIFramework
{
    public class UIDemoWindowController:WindowController,IController
    {
        public IArchitecture GetArchitecture()
        {
            return UIExampleDemo.Interface;
        }
    }
    

    public class UIDemoWindowController<T> : WindowController<T>,IController
        where T : WindowProperties
    {
        public IArchitecture GetArchitecture()
        {
            return UIExampleDemo.Interface;
        }
    }

    public class UIDemoPanelController:PanelController,IController
    {
        public IArchitecture GetArchitecture()
        {
            return UIExampleDemo.Interface;
        }
    }
}