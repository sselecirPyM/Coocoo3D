using System.Collections.Generic;

namespace Coocoo3DGraphics.Management;

public class DX12Resource
{
    public DX12SwapChainDescription[] SwapChainDescriptions;
    public DX12InputElementDescription[] InputElementDescriptions;
    public DX12InputLayoutDescription[] InputLayoutDescriptions;
    public DX12RenderTargetBlendDescription[] RenderTargetBlendDescriptions;

    public Dictionary<string, object> Exports;
}
