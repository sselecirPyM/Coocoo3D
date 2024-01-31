using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class _vertexBuffer : IDisposable
{
    public ID3D12Resource resource;
    public VertexBufferView vertexBufferView;
    public int Capacity;
    public int stride;
    public byte[] data;

    internal BufferTracking baseBuffer;
    internal int baseBufferOffset;

    public void Dispose()
    {
        resource?.Release();
        resource = null;
    }
}
public class Mesh : IDisposable
{
    public Mesh baseMesh;
    internal ID3D12Resource indexBuffer;

    internal Dictionary<string, _vertexBuffer> vtBuffers = new Dictionary<string, _vertexBuffer>();
    internal List<_vertexBuffer> vtBuffersDisposed = new List<_vertexBuffer>();

    public int m_indexCount;
    public int m_vertexCount;

    internal IndexBufferView indexBufferView;
    public int indexBufferCapacity;

    public byte[] m_indexData;

    public void AddBuffer<T>(ReadOnlySpan<T> verticeData, string slot) where T : unmanaged
    {
        ReadOnlySpan<byte> dat = MemoryMarshal.Cast<T, byte>(verticeData);

        var vertexBuffer = new _vertexBuffer();
        vertexBuffer.data = dat.ToArray();
        vertexBuffer.stride = vertexBuffer.data.Length / m_vertexCount;
        vtBuffers.Add(slot, vertexBuffer);
    }
    internal _vertexBuffer AddBuffer(string slot)
    {
        var bufDef = new _vertexBuffer();
        vtBuffers.Add(slot, bufDef);
        return bufDef;
    }

    public void LoadIndex<T>(int vertexCount, ReadOnlySpan<T> indexData) where T : unmanaged
    {
        foreach (var buf in vtBuffers.Values)
        {
            if (buf.resource != null)
                vtBuffersDisposed.Add(buf);
        }
        vtBuffers.Clear();
        this.m_vertexCount = vertexCount;
        if (indexData != null)
        {
            ReadOnlySpan<byte> d1 = MemoryMarshal.Cast<T, byte>(indexData);
            this.m_indexData = new byte[d1.Length];
            d1.CopyTo(this.m_indexData);
            this.m_indexCount = indexData.Length;
        }
    }

    public int GetIndexCount()
    {
        return m_indexCount;
    }

    public int GetVertexCount()
    {
        return m_vertexCount;
    }

    internal ID3D12Resource GetIndexBuffer()
    {
        return indexBuffer ?? baseMesh?.GetIndexBuffer();
    }

    internal _vertexBuffer GetVertexBuffer(string name)
    {
        if (vtBuffers.TryGetValue(name, out var buf))
        {
            return buf;
        }
        return baseMesh?.GetVertexBuffer(name);
    }

    public void Dispose()
    {
        foreach (var vtbuf in vtBuffers)
        {
            vtbuf.Value.Dispose();
        }
        vtBuffers.Clear();
        indexBuffer?.Release();
        indexBuffer = null;
    }
}
