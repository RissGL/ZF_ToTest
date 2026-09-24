using System;
using System.Collections.Generic;

namespace ZGameFramework.Core
{
    public static class EventBus
    {
        private static class EventRegistry<T> where T : GameEvent
        {
            public static readonly List<Action<T>> ParamListeners = new List<Action<T>>();

            public static readonly List<Action> SignalListeners = new List<Action>();
        }

        public static IUnregister Register<T>(Action<T> listener) where T : GameEvent
        {
            EventRegistry<T>.ParamListeners.Add(listener);
            return new CustomUnregister(() => EventBus.Unregister<T>(listener));
        }

        public static IUnregister Register<T>(Action listener) where T : GameEvent
        {
            EventRegistry<T>.SignalListeners.Add(listener);
            return new CustomUnregister(() => EventBus.Unregister<T>(listener));
        }

        public static void Unregister<T>(Action listener) where T : GameEvent
        {
            EventRegistry<T>.SignalListeners.Remove(listener);
        }

        public static void Unregister<T>(Action<T> listener) where T : GameEvent
        {
            EventRegistry<T>.ParamListeners.Remove(listener);
        }

        public static void Publish<T>(T eventData) where T : GameEvent
        {
            List<Action<T>> paramSnapshot = null;
            if (EventRegistry<T>.ParamListeners.Count > 0)
            {
                paramSnapshot = ListPool<Action<T>>.Get();
                paramSnapshot.AddRange(EventRegistry<T>.ParamListeners);
            }

            if (paramSnapshot != null)
            {
                foreach (var listener in paramSnapshot)
                    listener?.Invoke(eventData);
                ListPool<Action<T>>.Recycle(paramSnapshot);
            }
        }

        public static void PublishSignal<T>() where T : GameEvent
        {
            List<Action> snapshot = null;
            if (EventRegistry<T>.SignalListeners.Count > 0)
            {
                snapshot = ListPool<Action>.Get();
                snapshot.AddRange(EventRegistry<T>.SignalListeners);
            }

            if (snapshot != null)
            {
                foreach (var listener in snapshot)
                    listener?.Invoke();
                ListPool<Action>.Recycle(snapshot);
            }
        }
    }
}