namespace ZGameFramework.Core
{
    using System.Collections.Generic;

    public class CachePool<TKey, TValue>
    {
        public class CacheItem
        {
            public TKey Key;
            public TValue Value;

            public CacheItem(TKey key, TValue value)
            {
                this.Key = key;
                this.Value = value;
            }
        }

        private readonly object syncLock = new object();
        private readonly int capacity;
        private LinkedList<CacheItem> lruList;
        private Dictionary<TKey, LinkedListNode<CacheItem>> cacheDict;

        public CachePool(int capacity)
        {
            this.capacity = capacity > 0 ? capacity : 10;
            lruList = new LinkedList<CacheItem>();
            cacheDict = new Dictionary<TKey, LinkedListNode<CacheItem>>(this.capacity);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (syncLock)
            {
                if (cacheDict.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    value = node.Value.Value;
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            lock (syncLock)
            {
                if (cacheDict.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    node.Value.Value = value;
                    lruList.Remove(node);
                    lruList.AddFirst(node);
                }
                else
                {
                    if (cacheDict.Count >= capacity)
                    {
                        LinkedListNode<CacheItem> lastNode = lruList.Last;
                        cacheDict.Remove(lastNode.Value.Key);
                        lastNode.Value.Key = key;
                        lastNode.Value.Value = value;
                        lruList.Remove(lastNode);
                        lruList.AddFirst(lastNode);
                        cacheDict.Add(key, lastNode);
                    }
                    else
                    {
                        CacheItem newItem = new CacheItem(key, value);
                        LinkedListNode<CacheItem> newNode = new LinkedListNode<CacheItem>(newItem);
                        lruList.AddFirst(newNode);
                        cacheDict.Add(key, newNode);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (syncLock)
            {
                cacheDict.Clear();
                lruList.Clear();
            }
        }
    }
}
