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
            int RowPitch = ((m_width * bytesPerPixel + 255) & ~255);
            int targetRowPitch = m_width * bytesPerPixel;
            int imageSize = RowPitch * m_height;
            m_textureReadBack.Map(0, new Range(imageSize * index, imageSize * (index + 1)), &ptr);
            ptr = (byte*)ptr + imageSize * index;
            for (int i = 0; i < m_height; i++)
            {
                memcpy(bitmapData.Slice(targetRowPitch * i / size, targetRowPitch / size), (byte*)ptr + RowPitch * i, targetRowPitch);
            }
            m_textureReadBack.Unmap(0);
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
            m_textureReadBack?.Release();
            m_textureReadBack = null;
        }
        public ID3D12Resource m_textureReadBack;
        public int m_width;
        public int m_height;
        public int bytesPerPixel;
    }
}
