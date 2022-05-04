using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.ResourceWrap
{
    public class DictionaryWithModifiyIndex<TKey, TValue> : IDictionary<TKey, TValue>
    {
        Dictionary<TKey, TValue> internalDictionary = new Dictionary<TKey, TValue>();

        Dictionary<TKey, int> indices = new Dictionary<TKey, int>();

        public void SetModifyIndex(TKey key,int index1)
        {
            indices[key] = index1;
        }

        public int GetModifyIndex(TKey key)
        {
            indices.TryGetValue(key, out int v);
            return v;
        }

        public TValue this[TKey key] { get => ((IDictionary<TKey, TValue>)internalDictionary)[key]; set => ((IDictionary<TKey, TValue>)internalDictionary)[key] = value; }

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)internalDictionary).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)internalDictionary).Values;

        public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            ((IDictionary<TKey, TValue>)internalDictionary).Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).Clear();
            ((ICollection<KeyValuePair<TKey, int>>)indices).Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return ((IDictionary<TKey, TValue>)internalDictionary).ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)internalDictionary).GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            return ((IDictionary<TKey, TValue>)internalDictionary).Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)internalDictionary).Remove(item);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return ((IDictionary<TKey, TValue>)internalDictionary).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)internalDictionary).GetEnumerator();
        }
    }
}
