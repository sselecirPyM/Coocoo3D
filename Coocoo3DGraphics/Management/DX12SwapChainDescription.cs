using Vortice.DXGI;

namespace Coocoo3DGraphics.Management;

public class DX12SwapChainDescription
{
    //public int Width;
    //public int Height;
    public Format Format;
    public bool Stereo;
    //public SampleDescription SampleDescription;
    public Usage BufferUsage;
    public int BufferCount;
    public Scaling Scaling;
    public SwapEffect SwapEffect;
    public AlphaMode AlphaMode;
    public SwapChainFlags Flags;


    public SwapChainDescription1 GetSwapChainDescription1()
    {
        return new SwapChainDescription1
        {
            Format = Format,
            Stereo = Stereo,
            BufferUsage = BufferUsage,
            BufferCount = BufferCount,
            AlphaMode = AlphaMode,
            Flags = Flags,
            SwapEffect = SwapEffect,
            Scaling = Scaling,
            SampleDescription = new SampleDescription(1, 0)
        };
    }
}
