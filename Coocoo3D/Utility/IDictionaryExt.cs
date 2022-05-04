using Coocoo3D.FileFormat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class IDictionaryExt
    {
        public static T ConcurrenceGetOrCreate<T>(this IDictionary<string, T> iDictionary, string path) where T : new()
        {
            T val = default(T);
            lock (iDictionary)
            {
                if (!iDictionary.TryGetValue(path, out val))
                {
                    val = new T();
                    iDictionary.Add(path, val);
                }
            }
            return val;
        }
        public static T GetOrCreate<T>(this IDictionary<string, T> iDictionary, string path) where T : new()
        {
            T val = default(T);
            if (!iDictionary.TryGetValue(path, out val))
            {
                val = new T();
                iDictionary.Add(path, val);
            }
            return val;
        }
        public static T GetOrCreate<T1, T>(this IDictionary<T1, T> dict, T1 key, Func<T1, T> createFun)
        {
            if (dict.TryGetValue(key, out T v))
            {
                return v;
            }
            v = createFun(key);


            dict[key] = v;


            return v;
        }
        public static T GetOrCreate<T1, T>(this IDictionary<T1, T> dict, T1 key, Func<T> createFun)
        {
            if (dict.TryGetValue(key, out T v))
            {
                return v;
            }
            v = createFun();
            dict[key] = v;
            return v;
        }

        public static int Increase(this IDictionary<string, int> dict, string key)
        {
            dict.TryGetValue(key, out int val);
            dict[key] = val + 1;
            return val;
        }

        public static int RemoveWhere<T1, T2>(this IDictionary<T1, T2> dict, Predicate<KeyValuePair<T1, T2>> match)
        {
            var list = System.Buffers.MemoryPool<T1>.Shared.Rent(dict.Count).Memory;
            var span = list.Span;

            int removeCount = 0;
            foreach (var pair in dict)
            {
                if (match(pair))
                {
                    span[removeCount] = pair.Key;
                    removeCount++;
                }
            }
            for (int i = 0; i < removeCount; i++)
            {
                var key = span[i];
                dict.Remove(key);
            }
            return removeCount;
        }

        public static void AddRange<T1, T2>(this IDictionary<T1, T2> dict, IDictionary<T1, T2> add)
        {
            foreach (var pair in add)
            {
                dict[pair.Key] = pair.Value;
            }
        }

        public static bool TryGetTypedValue<T1, T2>(this IDictionary<T1, object> dict, T1 key, out T2 value)
        {
            if (dict.TryGetValue(key, out object obj) && obj is T2 x1)
            {
                value = x1;
                return true;
            }
            value = default(T2);
            return false;
        }
    }
}
