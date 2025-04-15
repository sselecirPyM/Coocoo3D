using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class LocalResourceProxy
{
    const int D3D12ShaderIdentifierSizeInBytes = 32;
    public byte[] buffer;
    public GraphicsContext graphicsContext;
    public ID3D12StateObjectProperties pRtsoProps;

    public Dictionary<int, int> srvs;
    public Dictionary<int, int> cbvs;

    public unsafe void SetShader(string name)
    {
        var span = new ReadOnlySpan<byte>(pRtsoProps.GetShaderIdentifier(name).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
        span.CopyTo(buffer);
    }

    public void SetCBV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), out ulong addr);
            var dest = new Span<byte>(buffer, cbv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetCBV<T>(int slot, T[] data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes<T>(data), out ulong addr);
            var dest = new Span<byte>(buffer, cbv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV(int slot, ulong gpuVirtualAddress)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var dest = new Span<byte>(buffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, gpuVirtualAddress);
        }
    }

    public void SetSRV(int slot, Texture2D texture)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var addr = graphicsContext.GetSRVHandle(texture).Ptr;
            var dest = new Span<byte>(buffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV(int slot, GPUBuffer gpuBuffer)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var addr = graphicsContext.GetSRVHandle(gpuBuffer).Ptr;
            var dest = new Span<byte>(buffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }

    public void SetSRV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), out ulong addr);
            var dest = new Span<byte>(buffer, srv, sizeof(ulong));
            MemoryMarshal.Write(dest, addr);
        }
    }
}

public class RTPSO : IDisposable
{
    public RayTracingShaderDescription[] rayGenShaders;
    public RayTracingShaderDescription[] hitGroups;
    public RayTracingShaderDescription[] missShaders;
    public string[] exports;
    public byte[] datas;
    public ResourceAccessType[] shaderAccessTypes;
    public ResourceAccessType[] localShaderAccessTypes;
    public ID3D12StateObject so;
    internal RootSignature globalRootSignature;
    internal RootSignature localRootSignature;
    public int localSize = D3D12ShaderIdentifierSizeInBytes;

    internal Dictionary<int, int> localSRV = new Dictionary<int, int>();
    internal Dictionary<int, int> localCBV = new Dictionary<int, int>();

    const int D3D12ShaderIdentifierSizeInBytes = 32;

    internal bool InitializeSO(ID3D12Device5 device)
    {
        if (exports == null || exports.Length == 0)
            return false;

        globalRootSignature?.Dispose();
        globalRootSignature = new RootSignature();
        globalRootSignature.RayTracing(shaderAccessTypes);
        globalRootSignature.Sign1(device);

        List<StateSubObject> stateSubObjects = new List<StateSubObject>();

        List<ExportDescription> exportDescriptions = new List<ExportDescription>();
        foreach (var export in exports)
            exportDescriptions.Add(new ExportDescription(export));

        stateSubObjects.Add(new StateSubObject(new DxilLibraryDescription(datas, exportDescriptions.ToArray())));
        stateSubObjects.Add(new StateSubObject(new HitGroupDescription("emptyhitgroup", HitGroupType.Triangles, null, null, null)));
        foreach (var hitGroup in hitGroups)
        {
            stateSubObjects.Add(new StateSubObject(new HitGroupDescription(hitGroup.name, HitGroupType.Triangles, hitGroup.anyHit, hitGroup.closestHit, hitGroup.intersection)));
        }
        if (localShaderAccessTypes != null)
        {
            localRootSignature?.Dispose();
            localRootSignature = new RootSignature();
            localRootSignature.LocalRootSignature(localShaderAccessTypes);
            localRootSignature.Sign1(device);
            localSize += localShaderAccessTypes.Length * 8;
            stateSubObjects.Add(new StateSubObject(new LocalRootSignature(localRootSignature.rootSignature)));
            string[] hitGroups = new string[this.hitGroups.Length];
            for (int i = 0; i < this.hitGroups.Length; i++)
                hitGroups[i] = this.hitGroups[i].name;
            stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], hitGroups)));
        }

        stateSubObjects.Add(new StateSubObject(new RaytracingShaderConfig(64, 20)));
        stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], exports)));
        stateSubObjects.Add(new StateSubObject(new RaytracingPipelineConfig(2)));
        stateSubObjects.Add(new StateSubObject(new GlobalRootSignature(globalRootSignature.rootSignature)));
        InitializeLocalResourceOffset();
        var result = device.CreateStateObject(new StateObjectDescription(StateObjectType.RaytracingPipeline, stateSubObjects.ToArray()), out so);
        if (result.Failure)
            return false;
        return true;
    }
    void InitializeLocalResourceOffset()
    {
        int cbvOffset = 0;
        int srvOffset = 0;

        int byteOffset = D3D12ShaderIdentifierSizeInBytes;
        foreach (var access in localShaderAccessTypes)
        {
            if (access == ResourceAccessType.CBV)
            {
                localCBV[cbvOffset] = byteOffset;
                byteOffset += 8;
                cbvOffset++;
            }
            else if (access == ResourceAccessType.SRV)
            {
                localSRV[srvOffset] = byteOffset;
                byteOffset += 8;
                srvOffset++;
            }
            else if (access == ResourceAccessType.SRVTable)
            {
                localSRV[srvOffset] = byteOffset;
                byteOffset += 8;
                srvOffset++;
            }
        }
    }

    public void Dispose()
    {
        so?.Release();
        so = null;
        globalRootSignature?.Dispose();
        globalRootSignature = null;
        localRootSignature?.Dispose();
        localRootSignature = null;
    }
}
