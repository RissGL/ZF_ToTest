using System.Linq;
using System.Runtime.InteropServices;

namespace ZGameFramework 
{
    public abstract class GameArchitecture<T>:IArchitecture
        where T : GameArchitecture<T>,new()
    {
        protected static T m_Architecture;

        public static IArchitecture Interface 
        {
            get 
            {
                if (m_Architecture == null) 
                {
                    InitArchitecture();
                }
                return m_Architecture;
            }
        }

        private static IOCContainer m_IOCContainer = new();
        private static bool m_Inited = false;

        private static void InitArchitecture() 
        {
            if (m_Architecture == null)
            {
                m_Architecture=new T();
                m_Architecture.OnInit();

                InitAll();
            }
        }

        protected abstract void OnInit();

        public static void InitAll() 
        {
            if(m_Inited)return;
            foreach (var model in m_IOCContainer.GetInstanceByType<IModel>())
            {
                InitOne(model);
            }

            foreach (var system in m_IOCContainer.GetInstanceByType<ISystem>())
            {
                InitOne(system);
            }
            m_Inited = true;
        }

        public static void InitOne(ICanInit init)
        {
            init.Init();
            init.Initialized = true;
        }

        public T GetSystem<T>() where T : class,ISystem
            => m_IOCContainer.Get<T>();
        public T GetModel<T>() where T : class,IModel
            => m_IOCContainer.Get<T>();
        public T GetUtility<T>()where T : class,IUtility
            =>m_IOCContainer.Get<T>();

        public void RegisterSystem<T>(T system) where T : ISystem
        {
            system.SetArchitecture(this);
            m_IOCContainer.Register(system);

            if (m_Inited) 
            {
                InitOne(system);
            }
        }
        public void RegisterModel<T>(T model) where T :  IModel
        {
            model.SetArchitecture(this);
            m_IOCContainer.Register(model);

            if (m_Inited) 
            {
                InitOne(model);
            }
        }
        public void RegisterUtility<T>(T utility) where T : IUtility
        {
            m_IOCContainer.Register(utility);
        }

        public void SendCommand<T>(T command)where T:ICommand
            =>ExecuteCommand(command);
        public TResult SendCommand<TResult>(ICommand<TResult> command)
            => ExecuteCommand<TResult>(command);

        protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            command.SetArchitecture(this);
            return command.Execute();
        }
        protected virtual void ExecuteCommand(ICommand command)
        {
            command.SetArchitecture(this);
            command.Execute();
        }

        public void Deinit()
        {
            OnDeinit();
            foreach (var system in m_IOCContainer.
                GetInstanceByType<ISystem>().Where(e=>e.Initialized))
            {
                system.DeInit();
            }

            foreach (var model in m_IOCContainer.
                GetInstanceByType<IModel>().Where(e => e.Initialized))
            {
                model.DeInit();
            }
            m_IOCContainer.Clear();
            m_Inited = false;
            m_Architecture=null;
        }

        protected virtual void OnDeinit() 
        {
        }
    }
}