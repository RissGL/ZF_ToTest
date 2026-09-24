using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZGameFramework.Core
{
    public class ResTable
    {
        private static readonly Dictionary<string, ResBase> table = new();

        public static int ResCount => table.Count;
        public static int ActiveResCount
        {
            get
            {
                int count = 0;
                foreach (var res in table.Values)
                {
                    if (res.RefCount > 0)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public static string BuildKey<T>(string path)
        {
            return $"{path}_{typeof(T).Name}";
        }

        public static ResBase GetOrCreateRes<T>(string path)
        {
            string key = BuildKey<T>(path);

            if (table.TryGetValue(key, out var res))
            {
                return res;
            }

            ResBase newRes = new(path, key);

            table.Add(key, newRes);

            return newRes;
        }

        public static void ReleaseRes(ResBase res)
        {
            if (res == null) return;
            if (table.TryGetValue(res.Key, out var cached))
            {
                res.Release();
                if (res.NeedDel && ReferenceEquals(res, cached))
                {
                    if (res.NeedDel) table.Remove(res.Key);
                }
            }
        }

        public static void ClearUnused()
        {
            var keys = ListPool<string>.Get();
            foreach (var k in table)
            {
                if (k.Value.IsLoading)
                {
                    continue;
                }
                if (k.Value.NeedDel)
                {
                    keys.Add(k.Value.Key);
                }
            }

            foreach (var key in keys)
            {
                table.Remove(key);
            }
            ListPool<string>.Recycle(keys);
        }

        public static void ClearAll()
        {
            table.Clear();
        }

        public static int GetRefCount<T>(string path)
        {
            string key = BuildKey<T>(path);
            if (table.TryGetValue(key, out var res))
            {
                return res.RefCount;
            }
            return 0;
        }
    }
}