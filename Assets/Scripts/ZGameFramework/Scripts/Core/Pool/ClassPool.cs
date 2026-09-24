namespace ZGameFramework.Core
{
    using System;
    using System.Collections.Generic;



    public class ClassPool<T> where T : class, new()
    {
        public static readonly Stack<T> pool = new Stack<T>();

        private static readonly object syncLock = new object();

        public static int MaxCapacity { get; set; } = 200;

        public static int Count
        {
            get { lock (syncLock) return pool.Count; }
        }

        public static int TotalCreated { get; private set; }
        public static int TotalRecycled { get; private set; }
        public static int TotalRetrieved { get; private set; }

        public static T Get()
        {
            T obj;
            lock (syncLock)
            {
                if (pool.Count > 0)
                {
                    obj =pool.Pop();
                    if (obj is IPoolable poolable)
                    {
                        poolable.OnGet();
                    }
                }
                else
                {
                    obj = new T();
                    TotalCreated++;
                }
                TotalRetrieved++;
            }

            return obj;
        }

        public static void Recycle(T obj)
        {
            if (obj == null)
            {
                return;
            }

            if (obj is IPoolable poolable)
            {
                poolable.OnRecycled();
            }

            lock (syncLock)
            {
                if (pool.Count >= MaxCapacity)
                {
                    return;
                }
                pool.Push(obj);
                TotalRecycled++;
            }
        }

        public static void WarmUp(int count)
        {
            int toCreate = count;
            lock (syncLock)
            {
                toCreate=Math.Min(toCreate, MaxCapacity - pool.Count);
            }


            for (int i = 0; i < toCreate; i++)
            {
                lock (syncLock)
                {
                    pool.Push(new T());
                    TotalCreated++;
                }
            }

        }

        public static void ResetStats()
        {
            lock (syncLock)
            {
                TotalCreated = 0;
                TotalRecycled = 0;
                TotalRetrieved = 0;
            }
        }

        public static void Clear()
        {
            lock (syncLock)
            {
                pool.Clear();
            }
        }
    }

}