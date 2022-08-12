using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;
using Range = Vortice.Direct3D12.Range;

namespace Coocoo3DGraphics
{
    public class ReadBackTexture2D : IDisposable
    {
        public void Reload(int width, int height, int bytesPerPixel)
        {
            m_width = width;
            m_height = height;
            this.bytesPerPixel = bytesPerPixel;
        }
        unsafe public void GetRaw<T>(int index, Span<T> bitmapData) where T : unmanaged
        {
            int size = Marshal.SizeOf(typeof(T));
            void* ptr = null;
            //m_textureReadBack[index].Map(0, new Range(0, bitmapData.Length * size), &ptr);
            int RowPitch = ((m_width * bytesPerPixel + 255) & ~255);
            m_textureReadBack[index].Map(0, new Range(0, RowPitch * m_height), &ptr);
            for (int i = 0; i < m_height; i++)
            {
                memcpy(bitmapData.Slice(m_width * i * bytesPerPixel / size, m_width * bytesPerPixel / size), (byte*)ptr + RowPitch * i, m_width * bytesPerPixel);
            }
            m_textureReadBack[index].Unmap(0);
        }

        unsafe public Span<T> StartRead<T>(int index)
        {
            int size = Marshal.SizeOf(typeof(T));
            void* ptr = null;
            m_textureReadBack[index].Map(0, new Range(0, ((m_width * bytesPerPixel + 255) & ~255) * m_height), &ptr);
            return new Span<T>(ptr, m_width * m_height * bytesPerPixel / size);
        }
        public void StopRead(int index)
        {
            m_textureReadBack[index].Unmap(0);
        }

        public int GetWidth()
        {
            return m_width;
        }
        public int GetHeight()
        {
            return m_height;
        }

        public void Dispose()
        {
            if (m_textureReadBack != null)
                foreach (var tex in m_textureReadBack)
                {
                    tex.Release();
                }
            m_textureReadBack = null;
        }

        public ID3D12Resource[] m_textureReadBack;
        public int m_width;
        public int m_height;
        public int bytesPerPixel;
    }
}
