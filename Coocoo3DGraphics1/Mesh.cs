using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    public class _vertexBuffer
    {
        public ID3D12Resource vertex;
        public VertexBufferView vertexBufferView;
        public int actualLength;
        public byte[] data;
    }
    public class Mesh : IDisposable
    {
        public ID3D12Resource indexBuffer;

        public Dictionary<int, _vertexBuffer> vtBuffers = new Dictionary<int, _vertexBuffer>();
        public List<_vertexBuffer> vtBuffersDisposed = new List<_vertexBuffer>();

        public int m_indexCount;
        public int m_vertexCount;

        public IndexBufferView indexBufferView;
        public int indexBufferCapacity;

        public byte[] m_indexData;

        public void AddBuffer<T>(Span<T> verticeData, int slot) where T : unmanaged
        {
            Span<byte> dat = MemoryMarshal.Cast<T, byte>(verticeData);
            byte[] verticeData1 = new byte[dat.Length];
            dat.CopyTo(verticeData1);
            var bufDef = new _vertexBuffer();
            bufDef.data = verticeData1;
            vtBuffers.Add(slot, bufDef);
        }
        internal _vertexBuffer AddBuffer(int slot)
        {
            var bufDef = new _vertexBuffer();
            vtBuffers.Add(slot, bufDef);
            return bufDef;
        }

        public void ReloadIndex<T>(int vertexCount, Span<T> indexData) where T : unmanaged
        {
            vtBuffersDisposed.AddRange(vtBuffers.Values);
            vtBuffers.Clear();
            this.m_vertexCount = vertexCount;
            if (indexData != null)
            {
                Span<byte> d1 = MemoryMarshal.Cast<T, byte>(indexData);
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

        public bool TryGetBuffer(int index,out byte[] data)
        {
            data = null;
            if (vtBuffers.TryGetValue(index, out var mesh))
            {
                data = mesh.data;
                return true;
            }
            else
                return false;
        }

        public void SetIndexFormat(Format format)
        {
            indexBufferView.Format = format;
        }

        public void Dispose()
        {
            indexBuffer?.Release();
            indexBuffer = null;
        }
    }
}
