using System;
using System.Collections.Generic;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public enum ResourceAccessType
{
    CBV,
    SRV,
    UAV,
    CBVTable,
    SRVTable,
    UAVTable,
}
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

    void MakeDescs(RootSignatureFlags flags, IReadOnlyList<ResourceAccessType> descs, int registerSpace = 0)
    {
        StaticSamplerDescription[] samplerDescription = null;
        if (flags != RootSignatureFlags.LocalRootSignature)
        {
            samplerDescription = new StaticSamplerDescription[4];
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
        }

        RootParameter1[] rootParameters = new RootParameter1[descs.Count];

        int cbvCount = 0;
        int srvCount = 0;
        int uavCount = 0;
        cbv.Clear();
        srv.Clear();
        uav.Clear();

        for (int i = 0; i < descs.Count; i++)
        {
            ResourceAccessType t = descs[i];
            switch (t)
            {
                case ResourceAccessType.CBV:
                    rootParameters[i] = new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(cbvCount, registerSpace), ShaderVisibility.All);
                    cbv[cbvCount] = i;
                    cbvCount++;
                    break;
                case ResourceAccessType.SRV:
                    rootParameters[i] = new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(srvCount, registerSpace), ShaderVisibility.All);
                    srv[srvCount] = i;
                    srvCount++;
                    break;
                case ResourceAccessType.UAV:
                    rootParameters[i] = new RootParameter1(RootParameterType.UnorderedAccessView, new RootDescriptor1(uavCount, registerSpace), ShaderVisibility.All);
                    uav[uavCount] = i;
                    uavCount++;
                    break;
                case ResourceAccessType.CBVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ConstantBufferView, 1, cbvCount, registerSpace)), ShaderVisibility.All);
                    cbv[cbvCount] = i;
                    cbvCount++;
                    break;
                case ResourceAccessType.SRVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, srvCount, registerSpace)), ShaderVisibility.All);
                    srv[srvCount] = i;
                    srvCount++;
                    break;
                case ResourceAccessType.UAVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.UnorderedAccessView, 1, uavCount, registerSpace)), ShaderVisibility.All);
                    uav[uavCount] = i;
                    uavCount++;
                    break;
            }
        }

        description1 = new RootSignatureDescription1(flags, rootParameters, samplerDescription);
    }

    public void Load(IReadOnlyList<ResourceAccessType> Descs)
    {
        var flags = RootSignatureFlags.AllowInputAssemblerInputLayout;
        MakeDescs(flags, Descs);
    }

    internal void LocalRootSignature(IReadOnlyList<ResourceAccessType> Descs)
    {
        var flags = RootSignatureFlags.LocalRootSignature;
        MakeDescs(flags, Descs, 1);
    }

    internal void RayTracing(IReadOnlyList<ResourceAccessType> Descs)
    {
        MakeDescs(RootSignatureFlags.None, Descs);
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
