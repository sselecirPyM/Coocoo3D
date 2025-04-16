using System;
using System.Collections.Generic;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

internal class RootSignature : IDisposable
{
    internal ID3D12RootSignature rootSignature;
    public string Name;

    internal RootSignatureDescription1 description1;
    internal Dictionary<int, int> cbv = new Dictionary<int, int>();
    internal Dictionary<int, int> srv = new Dictionary<int, int>();
    internal Dictionary<int, int> uav = new Dictionary<int, int>();

    internal ID3D12RootSignature GetRootSignature(GraphicsDevice graphicsDevice)
    {
        if (rootSignature == null)
            Sign1(graphicsDevice.device);
        return rootSignature;
    }
    internal ID3D12RootSignature GetRootSignature(ID3D12Device graphicsDevice)
    {
        if (rootSignature == null)
            Sign1(graphicsDevice);
        return rootSignature;
    }

    internal void Sign1(ID3D12Device device)
    {
        rootSignature?.Release();
        rootSignature = device.CreateRootSignature<ID3D12RootSignature>(0, description1);
    }

    static internal StaticSamplerDescription[] DefaultSamplerDescription()
    {
        var samplerDescription = new StaticSamplerDescription[4];
        samplerDescription[0] = new StaticSamplerDescription()
        {
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            BorderColor = StaticBorderColor.OpaqueBlack,
            ComparisonFunction = ComparisonFunction.Never,
            Filter = Filter.MinMagMipLinear,
            MipLODBias = 0,
            MaxAnisotropy = 0,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
            ShaderVisibility = ShaderVisibility.All,
            RegisterSpace = 0,
            ShaderRegister = 0,
        };
        samplerDescription[1] = samplerDescription[0] with
        {
            ShaderRegister = 1,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MaxAnisotropy = 16,
            Filter = Filter.Anisotropic
        };

        samplerDescription[2] = samplerDescription[0] with
        {
            ShaderRegister = 2,
            ComparisonFunction = ComparisonFunction.Less,
            Filter = Filter.ComparisonMinMagMipLinear,
        };
        samplerDescription[3] = samplerDescription[0] with
        {
            ShaderRegister = 3,
            Filter = Filter.MinMagMipPoint
        };
        return samplerDescription;
    }

    internal void FromDesc(RootSignatureDescription1 description)
    {
        this.description1 = description;
    }

    public void Dispose()
    {
        rootSignature?.Release();
        rootSignature = null;
    }
}
