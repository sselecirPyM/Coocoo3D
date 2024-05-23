using System;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

public delegate void TextureDataCallback(ReadBackCallbackData readBackCallbackData, ReadOnlySpan<byte> bytes);
public class ReadBackCallbackData
{
    internal TextureDataCallback callback;
    internal ID3D12Resource resource;
    internal ulong frame;
    internal int rowPitch;
    internal int offset;
    internal int imageSize;
    public int width;
    public int height;
    public Format format;
    public object tag;

    unsafe public void Call()
    {
        void* ptr = null;
        ThrowIfFailed(resource.Map(0, new Vortice.Direct3D12.Range(offset, imageSize + offset), &ptr));
        ptr = (byte*)ptr + offset;
        byte[] bytes = new byte[width * 4 * height];
        Span<byte> bitmapData = bytes;
        int targetRowPitch = width * 4;
        for (int i = 0; i < height; i++)
        {
            memcpy(bitmapData.Slice(targetRowPitch * i, targetRowPitch), (byte*)ptr + rowPitch * i, targetRowPitch);
        }
        callback(this, bitmapData);
        resource.Unmap(0);
    }
}
