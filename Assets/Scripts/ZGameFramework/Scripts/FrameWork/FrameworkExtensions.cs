using System;
using ZGameFramework.Core;

namespace ZGameFramework
{
    public static class CanExtension
    {
        public static T GetSystem<T>(this ICanGetSystem self) where T :class,ISystem
        {
            return self.GetArchitecture().GetSystem<T>();
        }

        public static T GetModel<T>(this ICanGetModel self) where T : class,IModel
        {
            return self.GetArchitecture().GetModel<T>();
        }

        public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility
        {
            return self.GetArchitecture().GetUtility<T>();
        }

        public static void SendCommand<T>(this ICanSendCommand self, T command)
            where T : class, ICommand
        {
            self.GetArchitecture().SendCommand(command);
        }
        
        public static void SendCommand<T>(this ICanSendCommand self)
            where T : class, ICommand,new()
        {
            self.GetArchitecture().SendCommand(new T());
        }

        public static TResult SendCommand<TResult>
            (this ICanSendCommand self,ICommand<TResult> command)
        {
            return self.GetArchitecture().SendCommand<TResult>(command);
        }

        public static void SendEvent<TEvent>
            (this ICanSendEvent self, TEvent gameEvent) where TEvent:GameEvent
        {
            EventBus.Publish<TEvent>(gameEvent);
        }
        public static void SendEvent<TEvent>
            (this ICanSendEvent self) where TEvent:GameEvent,new()
        {
            EventBus.PublishSignal<TEvent>();
        }

        public static IUnregister RegisterEvent<T>(this ICanRegisterEvent self, Action action)
            where T:GameEvent
        {
            return EventBus.Register<T>(action);
        }
        public static IUnregister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> action)
            where T : GameEvent
        {
            return EventBus.Register<T>(action);
        }

        public static void UnregisterEvent<T>(this ICanRegisterEvent self, Action action)
            where T : GameEvent
        {
            EventBus.Unregister<T>(action);
        }

        public static void UnregisterEvent<T>(this ICanRegisterEvent self, Action<T> action)
            where T : GameEvent
        {
            EventBus.Unregister<T>(action);
        }
    }

}

