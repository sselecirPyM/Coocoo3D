using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class ReadBackTexture2D
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
            IntPtr ptr = m_textureReadBack[index].Map(0, new Range(0, bitmapData.Length * size));
            memcpy(bitmapData, ptr.ToPointer(), bitmapData.Length * size);
            m_textureReadBack[index].Unmap(0);
        }

        unsafe public Span<T> StartRead<T>(int index)
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = m_textureReadBack[index].Map(0, new Range(0, m_width * m_height * bytesPerPixel));
            return new Span<T>(ptr.ToPointer(), m_width * m_height * bytesPerPixel / size);
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
        public ID3D12Resource[] m_textureReadBack;
        public int m_width;
        public int m_height;
        public int bytesPerPixel;
    }
}
