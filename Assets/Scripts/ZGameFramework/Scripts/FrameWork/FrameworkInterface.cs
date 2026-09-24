namespace ZGameFramework
{
    public interface IArchitecture
    {
        void RegisterSystem<T>(T system) where T : ISystem;
        void RegisterModel<T>(T model) where T : IModel;
        void RegisterUtility<T>(T utility) where T : IUtility;

        T GetSystem<T>() where T : class, ISystem;
        T GetModel<T>() where T : class, IModel;
        T GetUtility<T>() where T : class, IUtility;

        void SendCommand<T>(T command) where T : ICommand;
        TResult SendCommand<TResult>(ICommand<TResult> command);

        void Deinit();
    }

    #region Role
    public interface IBelongToArchitecture
    {
        IArchitecture GetArchitecture();
    }

    public interface IController:ICanGetUtility,ICanGetSystem,ICanGetModel,
        ICanSendCommand,IBelongToArchitecture,ICanRegisterEvent,
        ICanSendEvent{ }
    public interface ISystem:ICanInit,ICanGetModel,ICanSendCommand,
        ICanSendEvent ,ICanRegisterEvent,IBelongToArchitecture,ICanSetArchitecture
    { }
    public interface IModel:ICanInit,ICanGetUtility,ICanSendEvent
        ,IBelongToArchitecture,ICanSetArchitecture{ }
    public interface IUtility { }
    public interface  ICommand:ICanGetSystem, ICanGetModel, ICanGetUtility,
        ICanSendEvent, ICanSendCommand,IBelongToArchitecture,ICanSetArchitecture
    {
        void Execute();
    }

    public interface ICommand<TResult>: ICanGetSystem, ICanGetModel, ICanGetUtility,
    ICanSendEvent, ICanSendCommand, IBelongToArchitecture, ICanSetArchitecture
    {
        TResult Execute();
    }
    #endregion

    #region Ability
    public interface ICanInit 
    {
        bool Initialized { get; set; } 
        void Init();
        void DeInit();

    }
    public interface  ICanGetModel: IBelongToArchitecture
    {
        
    }
    public interface ICanGetSystem : IBelongToArchitecture { }
    public interface ICanGetUtility : IBelongToArchitecture { }
    public interface ICanSendCommand : IBelongToArchitecture { }
    public interface ICanSendEvent : IBelongToArchitecture { }
    public interface ICanRegisterEvent : IBelongToArchitecture { }
    public interface ICanSetArchitecture : IBelongToArchitecture
    {
        void SetArchitecture(IArchitecture architecture);
    }
    #endregion
}