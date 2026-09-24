using System;

namespace ZGameFramework
{
    public abstract class AbstractSystem : ISystem
    {
        private IArchitecture m_Architecture;

        public bool Initialized { get; set; }

        public  void DeInit()=>OnDeInit();

        protected virtual void OnDeInit()
        {
            
        }

        public  void Init()=>OnInit();
        
        protected abstract void OnInit();

        public IArchitecture GetArchitecture()
        {
            return m_Architecture;
        }

        public void SetArchitecture(IArchitecture architecture)
        {
            m_Architecture = architecture;
        }
    }

    public abstract class AbstractModel : IModel
    {
        private IArchitecture m_Architecture;
        public bool Initialized { get; set; }

        public void DeInit() => OnDeInit();

        protected virtual void OnDeInit()
        {

        }
        public void Init() => OnInit();

        protected abstract void OnInit();

        public IArchitecture GetArchitecture()
        {
            return m_Architecture;
        }

        public void SetArchitecture(IArchitecture architecture)
        {
            m_Architecture = architecture;
        }
    }

    public abstract class AbstractCommand : ICommand
    {
        private IArchitecture m_Architecture;


        public IArchitecture GetArchitecture()
        {
            return m_Architecture;
        }

        public void SetArchitecture(IArchitecture architecture)
        {
            m_Architecture = architecture;
        }

        public void Execute()
        {
            OnExecute();
        }

        protected abstract void OnExecute();
    }
}