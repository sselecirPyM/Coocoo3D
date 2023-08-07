using Vortice.Direct3D12;

namespace Coocoo3DGraphics.Management;

public class DX12SamplerDescription
{
    public Filter Filter;
    public TextureAddressMode AddressU;
    public TextureAddressMode AddressV;
    public TextureAddressMode AddressW;
    public float MipLODBias;
    public int MaxAnisotropy;
    public ComparisonFunction ComparisonFunction;
    public StaticBorderColor BorderColor;
    public float MinLOD;
    public float MaxLOD;
    public int ShaderRegister;
    public int RegisterSpace;
    public ShaderVisibility ShaderVisibility;

    public StaticSamplerDescription GetStaticSamplerDescription()
    {
        return new StaticSamplerDescription
        {
            Filter = Filter,
            AddressU = AddressU,
            AddressV = AddressV,
            AddressW = AddressW,
            BorderColor = BorderColor,
            MinLOD = MinLOD,
            MaxLOD = MaxLOD,
            ShaderRegister = ShaderRegister,
            RegisterSpace = RegisterSpace,
            ShaderVisibility = ShaderVisibility,
            ComparisonFunction = ComparisonFunction,
            MaxAnisotropy = MaxAnisotropy,
            MipLODBias = MipLODBias,
        };
    }
}
