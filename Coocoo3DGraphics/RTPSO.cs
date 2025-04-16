using Coocoo3DGraphics.Commanding;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Shader;

namespace Coocoo3DGraphics;

public class LocalResourceProxy
{
    const int D3D12ShaderIdentifierSizeInBytes = 32;
    public byte[] shaderTableBuffer;
    public GraphicsContext graphicsContext;
    public ID3D12StateObjectProperties pRtsoProps;

    public Dictionary<int, int> srvs;
    public Dictionary<int, int> cbvs;

    public RTPSO currentPSO;
    internal HitGroupDescription2 hitGroup;

    public unsafe void SetShader(string name)
    {
        if (currentPSO.hitGroupInstances.TryGetValue(name, out hitGroup))
        {
            shaderTableBuffer = new byte[hitGroup.size];
            srvs = hitGroup.localSRV;
            cbvs = hitGroup.localCBV;
            var span = new ReadOnlySpan<byte>(pRtsoProps.GetShaderIdentifier(name).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
            span.CopyTo(shaderTableBuffer);
        }
    }

    public void SetCBV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), out ulong addr);
            var dest = new Span<byte>(shaderTableBuffer, cbv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetCBV<T>(int slot, T[] data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes<T>(data), out ulong addr);
            var dest = new Span<byte>(shaderTableBuffer, cbv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }


    public void SetCBV(int slot, CBVAction action)
    {
        if (!hitGroup.cbvs.TryGetValue(slot, out var desc))
            return;

        if (cbvs.TryGetValue(slot, out var cbv))
        {
            byte[] cbvBuffer = new byte[desc.size];
            var proxy = new CBVProxy
            {
                positionMap = desc.positionMap,
                buffer = cbvBuffer
            };

            action(proxy);
            graphicsContext.readonlyBufferAllocator.Upload(cbvBuffer, out ulong addr);
            var dest = new Span<byte>(shaderTableBuffer, cbv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV(int slot, ulong gpuVirtualAddress)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var dest = new Span<byte>(shaderTableBuffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, gpuVirtualAddress);
        }
    }

    public void SetSRV(int slot, Texture2D texture)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var addr = graphicsContext.GetSRVHandle(texture).Ptr;
            var dest = new Span<byte>(shaderTableBuffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV(int slot, GPUBuffer gpuBuffer)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var addr = graphicsContext.GetSRVHandle(gpuBuffer).Ptr;
            var dest = new Span<byte>(shaderTableBuffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), out ulong addr);
            var dest = new Span<byte>(shaderTableBuffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }
}
internal class HitGroupDescription2
{
    internal RootSignature localRootSignature;
    internal int size;

    internal Dictionary<int, CBVDescription> cbvs = new Dictionary<int, CBVDescription>();
    internal Dictionary<int, int> localSRV = new Dictionary<int, int>();
    internal Dictionary<int, int> localCBV = new Dictionary<int, int>();
}

public class RTPSO : IDisposable
{
    public string[] rayGenShaders;
    public HitGroupDescription[] hitGroups;
    public string[] missShaders;
    public string[] exports;
    public byte[] datas;
    public ID3D12LibraryReflection libraryReflection;

    public ID3D12StateObject so;
    internal RootSignature globalRootSignature;
    internal Dictionary<int, CBVDescription> cbvs = new Dictionary<int, CBVDescription>();

    internal Dictionary<string, HitGroupDescription2> hitGroupInstances = new Dictionary<string, HitGroupDescription2>();


    const int D3D12ShaderIdentifierSizeInBytes = 32;

    internal bool InitializeSO(ID3D12Device5 device)
    {
        if (exports == null || exports.Length == 0)
            return false;

        globalRootSignature?.Dispose();

        int functionCount = libraryReflection.Description.FunctionCount;
        var array = new ID3D12FunctionReflection[functionCount];
        for (int i = 0; i < functionCount; i++)
        {
            array[i] = libraryReflection.GetFunctionByIndex(i);
        }
        CreateRootSignature(array, out globalRootSignature);
        globalRootSignature.Sign1(device);

        List<StateSubObject> stateSubObjects = new List<StateSubObject>();

        List<ExportDescription> exportDescriptions = new List<ExportDescription>();
        foreach (var export in exports)
            exportDescriptions.Add(new ExportDescription(export));

        stateSubObjects.Add(new StateSubObject(new DxilLibraryDescription(datas, exportDescriptions.ToArray())));
        stateSubObjects.Add(new StateSubObject(new Vortice.Direct3D12.HitGroupDescription("emptyhitgroup", HitGroupType.Triangles, null, null, null)));
        foreach (var hitGroup in hitGroups)
        {
            stateSubObjects.Add(new StateSubObject(new Vortice.Direct3D12.HitGroupDescription(hitGroup.name, HitGroupType.Triangles, hitGroup.anyHit, hitGroup.closestHit, hitGroup.intersection)));
            {
                var hitGroupDesc = new HitGroupDescription2();
                CreateLocalRootSignature(array, hitGroupDesc);
                var localRootSignature = hitGroupDesc.localRootSignature;
                localRootSignature.Sign1(device);

                InitializeLocalResourceOffset(hitGroupDesc);
                hitGroupInstances.Add(hitGroup.name, hitGroupDesc);
                stateSubObjects.Add(new StateSubObject(new LocalRootSignature(localRootSignature.rootSignature)));

                stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], hitGroup.name)));
            }
        }

        stateSubObjects.Add(new StateSubObject(new RaytracingShaderConfig(64, 20)));
        stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], exports)));
        stateSubObjects.Add(new StateSubObject(new RaytracingPipelineConfig(2)));
        stateSubObjects.Add(new StateSubObject(new GlobalRootSignature(globalRootSignature.rootSignature)));
        var result = device.CreateStateObject(new StateObjectDescription(StateObjectType.RaytracingPipeline, stateSubObjects.ToArray()), out so);
        if (result.Failure)
            return false;
        return true;
    }
    static void InitializeLocalResourceOffset(HitGroupDescription2 hitGroup)
    {
        var rs = hitGroup.localRootSignature;
        for (int i = 0; i < rs.description1.Parameters.Length; i++)
        {
            var p = rs.description1.Parameters[i];
            if (p.ParameterType == RootParameterType.DescriptorTable)
            {
                var range = p.DescriptorTable.Ranges[0];
                var r = range.BaseShaderRegister;
                var t = range.RangeType;
                switch (range.RangeType)
                {
                    case DescriptorRangeType.ConstantBufferView:
                        hitGroup.localCBV[r] = i * 8 + D3D12ShaderIdentifierSizeInBytes;
                        break;
                    case DescriptorRangeType.ShaderResourceView:
                        hitGroup.localSRV[r] = i * 8 + D3D12ShaderIdentifierSizeInBytes;
                        break;
                }
            }
            else
            {
                var r = p.Descriptor.ShaderRegister;
                var t = p.ParameterType;
                switch (p.ParameterType)
                {
                    case RootParameterType.ConstantBufferView:
                        hitGroup.localCBV[r] = i * 8 + D3D12ShaderIdentifierSizeInBytes;
                        break;
                    case RootParameterType.ShaderResourceView:
                        hitGroup.localSRV[r] = i * 8 + D3D12ShaderIdentifierSizeInBytes;
                        break;
                }
            }
        }
        hitGroup.size = D3D12ShaderIdentifierSizeInBytes + rs.description1.Parameters.Length * 8;
    }


    void CreateRootSignature(ID3D12FunctionReflection[] functionReflections, out RootSignature rootSignature1)
    {
        var parameters = new List<RootParameter1>();
        //var samplers = new List<StaticSamplerDescription>();
        rootSignature1 = new RootSignature();
        var constantBuffers = new Dictionary<string, ID3D12ShaderReflectionConstantBuffer>();
        foreach (var functionReflection in functionReflections)
        {
            Create(functionReflection, constantBuffers, rootSignature1.cbv, rootSignature1.srv, rootSignature1.uav, parameters, cbvs, (res) => res.Space == 0);
        }

        //var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), samplers.ToArray());
        var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), RootSignature.DefaultSamplerDescription());
        rootSignature1.FromDesc(rootSignatureDescription1);
    }
    static void CreateLocalRootSignature(ID3D12FunctionReflection[] functionReflections, HitGroupDescription2 hitGroupDescription2)
    {
        var parameters = new List<RootParameter1>();
        var rootSignature1 = new RootSignature();
        hitGroupDescription2.localRootSignature = rootSignature1;
        var constantBuffers = new Dictionary<string, ID3D12ShaderReflectionConstantBuffer>();
        foreach (var functionReflection in functionReflections)
        {
            Create(functionReflection, constantBuffers, rootSignature1.cbv, rootSignature1.srv, rootSignature1.uav, parameters, hitGroupDescription2.cbvs, (res) => res.Space == 1);
        }

        var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.LocalRootSignature, parameters.ToArray(), null);
        rootSignature1.FromDesc(rootSignatureDescription1);
    }

    static void Create(ID3D12FunctionReflection functionReflection,
        Dictionary<string, ID3D12ShaderReflectionConstantBuffer> constantBuffers,
        Dictionary<int, int> cbv,
        Dictionary<int, int> srv,
        Dictionary<int, int> uav,
        List<RootParameter1> parameters,
        Dictionary<int, CBVDescription> cbvDescriptions,
        Predicate<InputBindingDescription> filter)
    {
        var description = functionReflection.Description;
        for (int i = 0; i < description.ConstantBuffers; i++)
        {
            var constantBuffer = functionReflection.GetConstantBufferByIndex(i);
            constantBuffers[constantBuffer.Description.Name] = constantBuffer;
        }
        for (int i = 0; i < description.BoundResources; i++)
        {
            var res = functionReflection.GetResourceBindingDescription(i);
            if (!filter(res))
                continue;
            switch (res.Type)
            {
                case ShaderInputType.Rtaccelerationstructure:
                    if (srv.ContainsKey(res.BindPoint))
                        continue;
                    srv[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    break;
                case ShaderInputType.Texture:
                case ShaderInputType.Structured:
                    if (srv.ContainsKey(res.BindPoint))
                        continue;
                    srv[res.BindPoint] = parameters.Count;
                    if (res.Name.Contains("_addr"))
                    {
                        parameters.Add(new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    }
                    else
                    {
                        parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                        DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    }
                    break;
                case ShaderInputType.ConstantBuffer:
                    if (cbv.ContainsKey(res.BindPoint))
                        continue;
                    cbv[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    if (cbvDescriptions != null && constantBuffers.TryGetValue(res.Name, out var buffer))
                    {
                        var positionMap = new Dictionary<string, int>();
                        foreach (var item in buffer.Variables)
                        {
                            positionMap[item.Description.Name] = item.Description.StartOffset;
                        }
                        cbvDescriptions[res.BindPoint] = new CBVDescription()
                        {
                            positionMap = positionMap,
                            size = buffer.Description.Size,
                        };
                    }
                    break;
                case ShaderInputType.Sampler:
                    //samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                    //        0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                    break;
                case ShaderInputType.UnorderedAccessViewRWTyped:
                case ShaderInputType.UnorderedAccessViewRWStructured:
                    if (uav.ContainsKey(res.BindPoint))
                        continue;
                    uav[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                            DescriptorRangeType.UnorderedAccessView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                default:
                    break;
            }
        }
    }

    public void Dispose()
    {
        so?.Release();
        so = null;
        globalRootSignature?.Dispose();
        globalRootSignature = null;
        foreach (var v in hitGroupInstances.Values)
        {
            v.localRootSignature.Dispose();
        }
        hitGroupInstances.Clear();
    }
}
