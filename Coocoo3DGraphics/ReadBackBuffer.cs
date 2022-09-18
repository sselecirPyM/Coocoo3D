using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;
using Range = Vortice.Direct3D12.Range;

namespace Coocoo3DGraphics
{
    public class ReadBackBuffer : IDisposable
    {
        unsafe public void GetData<T>(int offset, int height, int RowPitch, int targetRowPitch, Span<T> bitmapData) where T : unmanaged
        {
            int size = Marshal.SizeOf(typeof(T));
            void* ptr = null;
            int imageSize = RowPitch * height;
            bufferReadBack.Map(0, new Range(offset, imageSize + offset), &ptr);
            ptr = (byte*)ptr + offset;
            for (int i = 0; i < height; i++)
            {
                memcpy(bitmapData.Slice(targetRowPitch * i / size, targetRowPitch / size), (byte*)ptr + RowPitch * i, targetRowPitch);
            }
            bufferReadBack.Unmap(0);
        }

        public void Dispose()
        {
            bufferReadBack?.Release();
            bufferReadBack = null;
        }
        public ID3D12Resource bufferReadBack;

        internal int GetOffsetAndMove(int size)
        {
            if (currentPosition + size > this.size)
            {
                currentPosition = 0;
            }
            int result = currentPosition;
            currentPosition = ((currentPosition + size + 255) & ~255) % this.size;
            return result;
        }

        public int size;
        public int currentPosition;
    }
}
