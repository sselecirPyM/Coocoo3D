using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Coocoo3DGraphics.Commanding
{
    public class CBVProxy
    {
        public byte[] buffer;
        public Dictionary<string, int> positionMap;
        public bool Set<T>(string key, T value) where T : unmanaged
        {
            if (positionMap.TryGetValue(key, out var index))
            {
                MemoryMarshal.Write<T>(buffer.AsSpan().Slice(index), value);
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Set<T>(string key, T[] value) where T : unmanaged
        {
            if (positionMap.TryGetValue(key, out var index))
            {
                MemoryMarshal.AsBytes(value.AsSpan()).CopyTo(buffer.AsSpan().Slice(index));
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Set<T>(string key, ReadOnlySpan<T> value) where T : unmanaged
        {
            if (positionMap.TryGetValue(key, out var index))
            {
                MemoryMarshal.AsBytes(value).CopyTo(buffer.AsSpan().Slice(index));
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
