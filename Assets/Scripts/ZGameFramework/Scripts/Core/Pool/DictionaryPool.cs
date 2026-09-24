using System.Collections.Generic;

namespace ZGameFramework.Core
{
    public class DictionaryPool<TKey, TValue>
    {
        private static readonly Stack<Dictionary<TKey, TValue>> pool = new Stack<Dictionary<TKey, TValue>>();
        private static readonly object syncLock = new object();

        public static Dictionary<TKey, TValue> Get()
        {
            lock (syncLock)
            {
                if (pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            return new Dictionary<TKey, TValue>();
        }

        public static void Recycle(Dictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                return;
            }
            dict.Clear();
            lock (syncLock)
            {
                pool.Push(dict);
            }
        }
    }
}
