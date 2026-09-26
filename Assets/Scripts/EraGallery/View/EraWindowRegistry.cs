using System.Collections.Generic;
using UnityEngine;

namespace ZF.EraGallery
{
    /// <summary>
    /// 场景里「时代 → 窗口」的登记处。窗口自己进来登记，别的功能想找某个时代的窗口就问它。
    ///
    /// 为什么要有它：人物要从一个时代搬到另一个时代，就得知道"目标时代的窗口在哪、把角色挂到哪个父节点下面"。
    /// 让 EraWorldController 暴露这个映射也行，但那样人物系统就得反过来依赖时代窗口的控制器；
    /// 用登记处两边都不用认识对方。
    /// </summary>
    public static class EraWindowRegistry
    {
        private static readonly Dictionary<EraId, EraWindow> Windows = new Dictionary<EraId, EraWindow>();

        /// <summary>关掉「Enter Play Mode 不重载域」时静态字段会留着，这里手动清一次。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Windows.Clear();

        public static void Register(EraWindow window)
        {
            if (window == null)
            {
                return;
            }

            Windows[window.Era] = window;
        }

        public static void Unregister(EraWindow window)
        {
            if (window == null)
            {
                return;
            }

            if (Windows.TryGetValue(window.Era, out EraWindow current) && current == window)
            {
                Windows.Remove(window.Era);
            }
        }

        public static EraWindow Get(EraId era) =>
            Windows.TryGetValue(era, out EraWindow window) ? window : null;

        public static IEnumerable<EraWindow> All => Windows.Values;

        public static int Count => Windows.Count;
    }
}
