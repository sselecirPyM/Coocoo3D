using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Coocoo3D.ResourceWrap;

public class VersionedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    Dictionary<TKey, TValue> internalDictionary = new Dictionary<TKey, TValue>();

    Dictionary<TKey, int> version = new Dictionary<TKey, int>();

    public void SetVersion(TKey key,int index1)
    {
        version[key] = index1;
    }

    public int GetVersion(TKey key)
    {
        version.TryGetValue(key, out int v);
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
        ((ICollection<KeyValuePair<TKey, int>>)version).Clear();
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
