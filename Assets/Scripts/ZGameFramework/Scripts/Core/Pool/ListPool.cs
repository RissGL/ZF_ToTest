using System.Collections.Generic;

namespace ZGameFramework.Core
{
    public class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();
        private static readonly object syncLock = new object();

        public static List<T> Get()
        {
            lock (syncLock)
            {
                if (pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            return new List<T>();
        }

        public static void Recycle(List<T> list)
        {
            if (list == null)
            {
                return;
            }
            list.Clear();
            lock (syncLock)
            {
                pool.Push(list);
            }
        }
    }
}
