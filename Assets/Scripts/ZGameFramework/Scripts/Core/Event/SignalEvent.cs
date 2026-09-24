/*using System;

namespace ZGameFramework.Core
{
    /// <summary>
    /// 无参事件基类，外部 Trigger调用，内部调用EventBus.PublishSignal发布事件
    /// WARNING: 所有监听者返回后事件即刻回收。
    /// 异步/协程使用请走 new，不要用池。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SignalEvent<T> : GameEvent where T:SignalEvent<T>, new()
    {
        private static readonly T Instance = new T();

        /// <summary>
        /// 外部调用订阅
        /// </summary>
        /// <param name="listener"></param>
        public static  void Register(Action listener)
        {
            EventBus.Register<T>(listener);
        }

        /// <summary>
        /// 外部调用取消订阅
        /// </summary>
        /// <param name="listener"></param>
        public static void Unregister(Action listener)
        {
            EventBus.Unregister<T>(listener);
        }

        public override void OnRecycled()
        {
            // Do nothing
        }

        public override void OnGet()
        {
            // Do nothing
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        public static void Publish() 
        {
            EventBus.PublishSignal<SignalEvent<T>>();
        }

    }


}*/