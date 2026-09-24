using UnityEngine;
using ZGameFramework;
using ZGameFramework.Core;

namespace ZGameFramework.Example
{

    public class ExampleSignalEvent : GameEvent { }

    public class ExampleParamEvent : GameEvent
    {
        public int PlayerId;
        public string Cause;
    }

    public class EventExampleUsage
    {
        public void Demo()
        {
            EventBus.PublishSignal<ExampleSignalEvent>();
            EventBus.Publish(new ExampleParamEvent { PlayerId = 1001, Cause = "Lava" });

            IUnregister h1 = EventBus.Register<ExampleSignalEvent>(() => Debug.Log("收到信号"));
            IUnregister h2 = EventBus.Register<ExampleParamEvent>(e => Debug.Log($"玩家 {e.PlayerId} 死于 {e.Cause}"));

            h1.Unregister();
        }
    }
}