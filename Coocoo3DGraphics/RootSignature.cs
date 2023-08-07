using System;
using System.Collections.Generic;
using System.Linq;
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
public class RootSignature : IDisposable
{
    public Dictionary<int, int> cbv = new Dictionary<int, int>();
    public Dictionary<int, int> srv = new Dictionary<int, int>();
    public Dictionary<int, int> uav = new Dictionary<int, int>();
    internal ID3D12RootSignature rootSignature;
    public string Name;

    public RootSignatureFlags flags = RootSignatureFlags.None;
    public ResourceAccessType[] descs;

    internal ID3D12RootSignature GetRootSignature(GraphicsDevice graphicsDevice)
    {
        if (rootSignature == null)
            Sign1(graphicsDevice);
        return rootSignature;
    }

    internal void Sign1(GraphicsDevice graphicsDevice, int registerSpace = 0)
    {
        //static samplers
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

        RootParameter1[] rootParameters = new RootParameter1[descs.Length];

        int cbvCount = 0;
        int srvCount = 0;
        int uavCount = 0;
        cbv.Clear();
        srv.Clear();
        uav.Clear();

        for (int i = 0; i < descs.Length; i++)
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

        Sign2(graphicsDevice, samplerDescription, rootParameters);
    }

    internal void Sign2(GraphicsDevice graphicsDevice, StaticSamplerDescription[] staticSamplerDescriptions, RootParameter1[] rootParameters)
    {
        RootSignatureDescription1 rootSignatureDescription = new RootSignatureDescription1();
        rootSignatureDescription.StaticSamplers = staticSamplerDescriptions;
        rootSignatureDescription.Flags = flags;
        rootSignatureDescription.Parameters = rootParameters;

        rootSignature?.Release();
        rootSignature = graphicsDevice.device.CreateRootSignature<ID3D12RootSignature>(0, rootSignatureDescription);
    }

    public void Load(IReadOnlyList<ResourceAccessType> Descs)
    {
        descs = Descs.ToArray();
        flags = RootSignatureFlags.AllowInputAssemblerInputLayout;
    }

    public void LoadCompute(IReadOnlyList<ResourceAccessType> Descs)
    {
        descs = Descs.ToArray();
        flags = RootSignatureFlags.AllowInputAssemblerInputLayout;
    }

    internal void ReloadLocalRootSignature(IReadOnlyList<ResourceAccessType> Descs)
    {
        descs = Descs.ToArray();
        flags = RootSignatureFlags.LocalRootSignature;
    }

    internal void ReloadRayTracing(IReadOnlyList<ResourceAccessType> Descs)
    {
        descs = Descs.ToArray();
    }

    public void Dispose()
    {
        rootSignature?.Release();
        rootSignature = null;
    }
}
