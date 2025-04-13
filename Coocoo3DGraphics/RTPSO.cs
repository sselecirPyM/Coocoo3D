using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

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

public class ComputeResourceProxy
{
    public GraphicsContext graphicsContext;

    public Dictionary<int, int> srvs;
    public Dictionary<int, int> cbvs;
    public Dictionary<int, int> uavs;

    public void SetCBV(int slot, ulong gpuVirtualAddress)
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.m_commandList.SetComputeRootConstantBufferView(cbv, gpuVirtualAddress);
        }
    }

    public void SetCBV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), 256, out ulong addr);
            graphicsContext.m_commandList.SetComputeRootConstantBufferView(cbv, addr);
        }
    }

    public void SetCBV<T>(int slot, T[] data) where T : unmanaged
    {
        if (cbvs.TryGetValue(slot, out var cbv))
        {
            graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes<T>(data), 256, out ulong addr);
            graphicsContext.m_commandList.SetComputeRootConstantBufferView(cbv, addr);
        }
    }

    public void SetSRV(int slot, ulong gpuVirtualAddress)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            graphicsContext.m_commandList.SetComputeRootShaderResourceView(srv, gpuVirtualAddress);
        }
    }

    public void SetSRV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var handle = graphicsContext.readonlyBufferAllocator.GetSRV(MemoryMarshal.AsBytes(data));
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(srv, handle);
        }
    }

    public void SetSRV(int slot, RTTopLevelAcclerationStruct tlas)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            graphicsContext.m_commandList.SetComputeRootShaderResourceView(srv, tlas.GPUVirtualAddress);
        }
    }

    public void SetSRV(int slot, Texture2D texture, bool linear = false)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var handle = graphicsContext.GetSRVHandle(texture, linear);
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(srv, handle);
        }
    }

    public void SetSRV(int slot, GPUBuffer gpuBuffer)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var handle = graphicsContext.GetSRVHandle(gpuBuffer);
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(srv, handle);
        }
    }

    public void SetSRV(int slot, Mesh mesh, string bufferName)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            var buffer = mesh.GetVertexBuffer(bufferName);
            ID3D12Resource resource;
            ulong firstElement = 0;
            if (buffer.baseBuffer != null)
            {
                resource = buffer.baseBuffer.resource;
                buffer.baseBuffer.ToState(graphicsContext.m_commandList, ResourceStates.Common);
                firstElement = (ulong)buffer.baseBufferOffset / 4;
            }
            else
            {
                resource = buffer.resource;
            }
            var handle = graphicsContext.CreateSRV(resource, new ShaderResourceViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer,
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                Buffer = new BufferShaderResourceView
                {
                    FirstElement = firstElement,
                    NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                    Flags = BufferShaderResourceViewFlags.Raw,
                }
            });
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(srv, handle);
        }
    }

    public void SetSRVMip(int slot, Texture2D texture, int mips)
    {
        if (srvs.TryGetValue(slot, out var srv))
        {
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(srv, graphicsContext.GetSRVHandleWithMip(texture, mips));
        }
    }

    public void SetUAV(int slot, Texture2D texture)
    {
        if (uavs.TryGetValue(slot, out var uav))
        {
            var handle = graphicsContext.GetUAVHandle(texture);
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(uav, handle);
        }
    }

    public void SetUAVMip(int slot, Texture2D texture, int mipIndex)
    {
        if (uavs.TryGetValue(slot, out var uav))
        {
            texture.SetPartResourceState(graphicsContext.m_commandList, ResourceStates.UnorderedAccess, mipIndex, 1);
            if (!(mipIndex < texture.mipLevels))
            {
                throw new ArgumentOutOfRangeException();
            }
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = texture.uavFormat,
            };
            if (texture.isCube)
            {
                uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2DArray;
                uavDesc.Texture2DArray = new Texture2DArrayUnorderedAccessView() { MipSlice = mipIndex, ArraySize = 6 };
            }
            else
            {
                uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2D;
                uavDesc.Texture2D = new Texture2DUnorderedAccessView { MipSlice = mipIndex };
            }

            graphicsContext.m_commandList.SetComputeRootDescriptorTable(uav, graphicsContext.CreateUAV(texture.resource, uavDesc));
        }
    }

    public void SetUAV(int slot, GPUBuffer gpuBuffer)
    {
        if (uavs.TryGetValue(slot, out var uav))
        {
            var handle = graphicsContext.GetUAVHandle(gpuBuffer);
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(uav, handle);
        }
    }
    public void SetUAV(int slot, Mesh mesh, string bufferName)
    {
        if (uavs.TryGetValue(slot, out var uav))
        {
            var buffer = mesh.GetVertexBuffer(bufferName);
            buffer.baseBuffer.ToState(graphicsContext.m_commandList, ResourceStates.UnorderedAccess);

            var handle = graphicsContext.CreateUAV(buffer.baseBuffer.resource, new UnorderedAccessViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView
                {
                    FirstElement = (ulong)buffer.baseBufferOffset / 4,
                    NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                    Flags = BufferUnorderedAccessViewFlags.Raw,
                }
            });
            graphicsContext.m_commandList.SetComputeRootDescriptorTable(uav, handle);
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
