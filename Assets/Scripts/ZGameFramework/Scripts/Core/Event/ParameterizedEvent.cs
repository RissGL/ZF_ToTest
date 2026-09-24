/*using System;

namespace ZGameFramework.Core
{
    /// WARNING: 所有监听者返回后事件即刻回收。
    /// 异步/协程使用请走 TriggerAsync，不要用Trigger。
    public abstract class ParameterizedEvent<T> : GameEvent where T : ParameterizedEvent<T>, new()
    {
        /// <summary>
        /// 子类实现：重置所有数据字段
        /// </summary>
        public abstract override void OnRecycled();
        public abstract override void OnGet();

        /// <summary>
        ///  外部调用订阅
        /// </summary>
        /// <param name="listener"></param>
        public static void Register(Action<T> listener)
        {
            EventBus.Register<T>(listener);
        }

        /// <summary>
        ///  外部调用取消订阅
        /// </summary>
        /// <param name="listener"></param>
        public static void Unregister(Action<T> listener)
        {
            EventBus.Unregister<T>(listener);
        }

        /// <summary>
        /// 使用对象池创建事件实例，执行初始化操作，然后发布并自动回收。
        /// 如果参数简单，子类可以添加多种 Trigger 重载，进一步简化调用
        /// 例：public static void Trigger(int id, string name)
        /// </summary>
        public static void Publish(Action<T> initializer)
        {
            var evt = ClassPool<T>.Get();
#if UNITY_EDITOR
            evt._recycled = false;
#endif
            initializer?.Invoke(evt);
            try 
            {
                EventBus.Publish((T)evt);
            }
            finally
            {
#if UNITY_EDITOR
                evt._recycled = true;
#endif
                ClassPool<T>.Recycle(evt);
            }
        }

        /// <summary>
        /// 异步安全发布：不走对象池，直接 new，不回收。
        /// 返回事件实例供调用方异步持有，用完自行丢弃。
        /// </summary>
        public static void PublishAsync(Action<T> initializer)
        {
            var evt = new T();
#if UNITY_EDITOR
            evt._recycled = false;
#endif
            initializer?.Invoke(evt);
            EventBus.Publish((T)evt);
        }

        /// <summary>
        /// 异步发布，外部必须手动回收,用于异步高频事件避免GC
        /// </summary>
        /// <param name="initializer"></param>
        public static T PublishAsyncRecycle(Action<T> initializer)
        {
            var evt = ClassPool<T>.Get();
#if UNITY_EDITOR
            evt._recycled = false;
#endif
            initializer?.Invoke(evt);
            EventBus.Publish((T)evt);
            return evt;
        }
    }
 }*/

    
