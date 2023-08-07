using Vortice.Direct3D12;

namespace Coocoo3DGraphics.Management;

public class DX12RenderTargetBlendDescription
{
    public bool BlendEnable;
    public bool LogicOpEnable;
    public Blend SrcBlend;
    public Blend DestBlend;
    public BlendOperation BlendOp;
    public Blend SrcBlendAlpha;
    public Blend DestBlendAlpha;
    public BlendOperation BlendOpAlpha;
    public LogicOp LogicOp;
    public ColorWriteEnable RenderTargetWriteMask;

    public RenderTargetBlendDescription GetDescription()
    {
        return new RenderTargetBlendDescription()
        {
            BlendEnable = BlendEnable,
            LogicOpEnable = LogicOpEnable,
            SrcBlendAlpha = SrcBlendAlpha,
            DestBlendAlpha = DestBlendAlpha,
            BlendOpAlpha = BlendOpAlpha,
            BlendOp = BlendOp,
            DestBlend = DestBlend,
            LogicOp = LogicOp,
            RenderTargetWriteMask = RenderTargetWriteMask,
            SrcBlend = SrcBlend,
        };
    }
}
