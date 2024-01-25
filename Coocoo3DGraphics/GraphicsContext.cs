using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

public sealed class GraphicsContext
{
    const int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

    GraphicsDevice graphicsDevice;
    ID3D12GraphicsCommandList4 m_commandList;
    ID3D12GraphicsCommandList4 m_copyCommandList;
    internal RootSignature currentRootSignature;
    RootSignature _currentGraphicsRootSignature;
    RootSignature _currentComputeRootSignature;

    public Dictionary<int, object> slots = new Dictionary<int, object>();

    public RTPSO currentRTPSO;
    public PSO currentPSO;
    //public PSODesc currentPSODesc;
    //public bool psoChange;

    public Dictionary<int, ulong> currentCBVs = new Dictionary<int, ulong>();
    public Dictionary<int, ulong> currentSRVs = new Dictionary<int, ulong>();
    public Dictionary<int, ulong> currentUAVs = new Dictionary<int, ulong>();

    public Dictionary<string, VertexBufferView> currentMesh = new Dictionary<string, VertexBufferView>();

    bool meshChanged;

    public int TriangleCount { get; private set; }

    public void Initialize(GraphicsDevice device)
    {
        this.graphicsDevice = device;
    }

    public bool SetPSO(ComputeShader computeShader)
    {
        if (!computeShader.TryGetPipelineState1(graphicsDevice.device, out var rootSignature, out var pipelineState))
        {
            return false;
        }
        m_commandList.SetComputeRootSignature(rootSignature.rootSignature);
        m_commandList.SetPipelineState(pipelineState);
        currentRootSignature = rootSignature;
        Reference(pipelineState);
        Reference(rootSignature.rootSignature);
        return true;
    }

    public bool SetPSO(PSO pso, in PSODesc desc)
    {
        if (!pso.TryGetPipelineState(graphicsDevice.device, desc, out var pipelineState, out var rootSignature))
        {
            return false;
        }
        m_commandList.SetGraphicsRootSignature(rootSignature.rootSignature);
        m_commandList.SetPipelineState(pipelineState);
        currentRootSignature = rootSignature;
        currentPSO = pso;
        //currentPSODesc = desc;
        Reference(pipelineState);
        Reference(rootSignature.rootSignature);
        //psoChange = true;
        return true;
    }

    public bool SetPSO(RTPSO pso)
    {
        if (!graphicsDevice.IsRayTracingSupport() || pso == null)
            return false;

        if (pso.so == null)
        {
            if (!pso.InitializeSO(graphicsDevice.device))
                return false;
        }
        SetRootSignature(pso.globalRootSignature);
        currentRTPSO = pso;
        m_commandList.SetPipelineState1(pso.so);
        return true;
    }

    void SetRTTopAccelerationStruct(RTTopLevelAcclerationStruct accelerationStruct)
    {
        if (accelerationStruct.initialized)
            return;
        accelerationStruct.initialized = true;
        if (graphicsDevice.scratchResource == null)
        {
            CreateUAVBuffer(134217728, ref graphicsDevice.scratchResource, ResourceStates.UnorderedAccess);
        }
        int instanceCount = accelerationStruct.instances.Count;
        Span<RaytracingInstanceDescription> raytracingInstanceDescriptions = stackalloc RaytracingInstanceDescription[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            if (BuildBTAS(accelerationStruct.instances[i], i, out var raytracingInstanceDescription))
            {
                raytracingInstanceDescriptions[i] = raytracingInstanceDescription;
            }
        }
        GetRingBuffer().UploadBuffer<RaytracingInstanceDescription>(raytracingInstanceDescriptions, out ulong gpuAddr);
        var tpInputs = new BuildRaytracingAccelerationStructureInputs
        {
            Layout = ElementsLayout.Array,
            Type = RaytracingAccelerationStructureType.TopLevel,
            DescriptorsCount = accelerationStruct.instances.Count,
            InstanceDescriptions = gpuAddr
        };

        var info1 = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(tpInputs);
        CreateUAVBuffer((int)info1.ResultDataMaxSizeInBytes, ref accelerationStruct.resource, ResourceStates.RaytracingAccelerationStructure);
        Reference(accelerationStruct.resource);
        var trtas = new BuildRaytracingAccelerationStructureDescription
        {
            Inputs = tpInputs,
            DestinationAccelerationStructureData = accelerationStruct.resource.GPUVirtualAddress,
            ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress
        };
        m_commandList.BuildRaytracingAccelerationStructure(trtas);
    }

    bool BuildBTAS(RTInstance instance, int instantID, out RaytracingInstanceDescription raytracingInstanceDescription)
    {
        string POSITION = "POSITION0";
        var btas = instance.accelerationStruct;
        var mesh = btas.mesh;
        if (btas.initialized)
        {
            raytracingInstanceDescription = default;
            return false;
        }

        var indexBuffer = mesh.GetIndexBuffer();
        var positionBuffer = mesh.GetVertexBuffer(POSITION);

        ulong position = positionBuffer.vertexBufferView.BufferLocation + (ulong)btas.vertexStart * 12;

        var inputs = new BuildRaytracingAccelerationStructureInputs
        {
            Type = RaytracingAccelerationStructureType.BottomLevel,
            Layout = ElementsLayout.Array,
            GeometryDescriptions = new RaytracingGeometryDescription[]
            {
                new RaytracingGeometryDescription(new RaytracingGeometryTrianglesDescription(new GpuVirtualAddressAndStride(position, 12),
                Format.R32G32B32_Float,
                btas.vertexCount,
                0,
                indexBuffer.GPUVirtualAddress + (ulong)btas.indexStart * 4,
                Format.R32_UInt,
                btas.indexCount)),
            },
            DescriptorsCount = 1
        };

        if (positionBuffer.vertex != null)
            Reference(positionBuffer.vertex);
        Reference(indexBuffer);
        var info = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

        CreateUAVBuffer((int)info.ResultDataMaxSizeInBytes, ref btas.resource, ResourceStates.RaytracingAccelerationStructure);
        var brtas = new BuildRaytracingAccelerationStructureDescription
        {
            Inputs = inputs,
            ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress,
            DestinationAccelerationStructureData = btas.resource.GPUVirtualAddress
        };

        m_commandList.BuildRaytracingAccelerationStructure(brtas);
        m_commandList.ResourceBarrierUnorderedAccessView(btas.resource);
        Reference(btas.resource);
        raytracingInstanceDescription = new RaytracingInstanceDescription
        {
            AccelerationStructure = btas.resource.GPUVirtualAddress,
            InstanceContributionToHitGroupIndex = (Vortice.UInt24)(uint)instantID,
            InstanceID = (Vortice.UInt24)(uint)instantID,
            InstanceMask = instance.instanceMask,
            Transform = GetMatrix3X4(Matrix4x4.Transpose(instance.transform))
        };
        btas.initialized = true;
        return true;
    }
    const int D3D12ShaderIdentifierSizeInBytes = 32;

    public void DispatchRays(int width, int height, int depth, RayTracingCall call)
    {
        SetRTTopAccelerationStruct(call.tpas);
        var pRtsoProps = currentRTPSO.so.QueryInterface<ID3D12StateObjectProperties>();
        Reference(currentRTPSO.so);

        currentRootSignature = currentRTPSO.globalRootSignature;
        SetSRVRSlot(0, call.tpas.resource.GPUVirtualAddress);

        WriteGlobalHandles(call);

        var dispatchRaysDescription = new DispatchRaysDescription
        {
            Width = width,
            Height = height,
            Depth = depth,
        };

        {
            GetRingBuffer().UploadBuffer(GetShaderIdentifier(pRtsoProps, call.rayGenShader), out var gpuaddr);
            dispatchRaysDescription.RayGenerationShaderRecord = new GpuVirtualAddressRange(gpuaddr, (ulong)D3D12ShaderIdentifierSizeInBytes);
        }

        MemoryStream memoryStream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(memoryStream);
        writer.Seek(0, SeekOrigin.Begin);
        string[] nameIndex = new string[] { "POSITION0", "NORMAL0", "TEXCOORD0" };
        foreach (var inst in call.tpas.instances)
        {
            if (inst.hitGroupName != null)
            {
                var blas = inst.accelerationStruct;
                var mesh = blas.mesh;
                var indexBuffer = mesh.GetIndexBuffer();
                writer.Write(GetShaderIdentifier(pRtsoProps, inst.hitGroupName));
                writer.Write(indexBuffer.GPUVirtualAddress + (ulong)blas.indexStart * 4);
                int vertexStart = blas.vertexStart;

                for (int i = 0; i < nameIndex.Length; i++)
                {
                    string name = nameIndex[i];
                    var buffer = mesh.GetVertexBuffer(name);
                    writer.Write(buffer.vertexBufferView.BufferLocation + (ulong)(buffer.vertexBufferView.StrideInBytes * vertexStart));
                }

                WriteLocalHandles(inst, currentRTPSO, writer);
                BufferAlign(writer, 64);
            }
            else
            {
                writer.Write(GetShaderIdentifier(pRtsoProps, "emptyhitgroup"));
                for (int i = 0; i < currentRTPSO.localSize - D3D12ShaderIdentifierSizeInBytes; i++)
                {
                    writer.Write((byte)0);
                }
                BufferAlign(writer, 64);
            }
        }
        if (memoryStream.Position > 0)
        {
            int length1 = (int)memoryStream.Position;
            GetRingBuffer().UploadBuffer(new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, length1), out var gpuaddr);
            dispatchRaysDescription.HitGroupTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, (ulong)(length1 / call.tpas.instances.Count));
        }

        writer.Seek(0, SeekOrigin.Begin);
        if (call.missShaders != null && call.missShaders.Length > 0)
        {
            foreach (var missShader in call.missShaders)
            {
                writer.Write(GetShaderIdentifier(pRtsoProps, missShader));
            }

            int length1 = (int)memoryStream.Position;
            GetRingBuffer().UploadBuffer(new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, length1), out var gpuaddr);
            dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, D3D12ShaderIdentifierSizeInBytes);
        }

        writer.Seek(0, SeekOrigin.Begin);
        pRtsoProps.Dispose();
        PipelineBindingCompute();
        m_commandList.DispatchRays(dispatchRaysDescription);
    }

    static unsafe ReadOnlySpan<byte> GetShaderIdentifier(ID3D12StateObjectProperties pRtsoProps, string shader)
    {
        return new ReadOnlySpan<byte>(pRtsoProps.GetShaderIdentifier(shader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
    }

    void WriteGlobalHandles(RayTracingCall call)
    {
        int cbvOffset = 0;
        int srvOffset = 0;
        int uavOffset = 0;
        foreach (var access in currentRTPSO.shaderAccessTypes)
        {
            if (access == ResourceAccessType.SRV)
            {
                srvOffset++;
            }
            else if (access == ResourceAccessType.SRVTable)
            {
                if (call.SRVs != null && call.SRVs.TryGetValue(srvOffset, out object srv0))
                {
                    if (srv0 is IGPUResource gpuResource)
                        SetSRVTSlot(srvOffset, gpuResource);
                }
                srvOffset++;
            }
            else if (access == ResourceAccessType.CBV)
            {
                if (call.CBVs != null && call.CBVs.TryGetValue(cbvOffset, out object cbv0))
                {
                    if (cbv0 is byte[] cbvData)
                        SetCBVRSlot<byte>(cbvOffset, cbvData);
                    else if (cbv0 is Matrix4x4[] cbvDataM)
                        SetCBVRSlot<Matrix4x4>(cbvOffset, cbvDataM);
                    else if (cbv0 is Vector4[] cbvDataF4)
                        SetCBVRSlot<Vector4>(cbvOffset, cbvDataF4);
                }
                cbvOffset++;
            }
            else if (access == ResourceAccessType.UAVTable)
            {
                if (call.UAVs != null && call.UAVs.TryGetValue(uavOffset, out object uav0))
                {
                    if (uav0 is Texture2D tex2d)
                        SetRTSlot(uavOffset, tex2d);
                    else if (uav0 is GPUBuffer buffer)
                        SetUAVTSlot(uavOffset, buffer);
                }
                uavOffset++;
            }
        }
    }

    void WriteLocalHandles(RTInstance inst, RTPSO rtpso, BinaryWriter writer)
    {
        int cbvOffset = 0;
        int srvOffset = 0;
        foreach (var access in rtpso.localShaderAccessTypes)
        {
            if (access == ResourceAccessType.CBV)
            {
                if (inst.CBVs != null && inst.CBVs.TryGetValue(cbvOffset, out object cbv0))
                {
                    if (cbv0 is byte[] cbvData)
                        _RTWriteGpuAddr<byte>(cbvData, writer);
                    else if (cbv0 is Matrix4x4[] cbvDataM)
                        _RTWriteGpuAddr<Matrix4x4>(cbvDataM, writer);
                    else if (cbv0 is Vector4[] cbvDataF4)
                        _RTWriteGpuAddr<Vector4>(cbvDataF4, writer);
                    else
                        writer.Write((ulong)0);
                }
                else
                    writer.Write((ulong)0);
                cbvOffset++;
            }
            else if (access == ResourceAccessType.SRV)
            {
                srvOffset++;
            }
            else if (access == ResourceAccessType.SRVTable)
            {
                if (inst.SRVs != null && inst.SRVs.TryGetValue(srvOffset, out object srv0))
                {
                    if (srv0 is Texture2D tex2d)
                        writer.Write(GetSRVHandle(tex2d).Ptr);
                    else if (srv0 is GPUBuffer buffer)
                        writer.Write(GetSRVHandle(buffer).Ptr);
                    else if (srv0 is ID3D12Resource resource)
                        writer.Write(ReferenceGetAddr(resource));
                    else
                        writer.Write((ulong)0);
                }
                else
                    writer.Write((ulong)0);
                srvOffset++;
            }
        }
    }
    void BufferAlign(BinaryWriter writer, int align)
    {
        var stream = writer.BaseStream;
        var newPos = align_to(align, (int)stream.Position) - (int)stream.Position;
        for (int k = 0; k < newPos; k++)
        {
            writer.Write((byte)0);
        }
    }

    public void SetSRVTSlotLinear(int slot, Texture2D texture) => currentSRVs[slot] = GetSRVHandle(texture, true).Ptr;

    public void SetSRVTSlot(int slot, IGPUResource resource)
    {
        switch (resource)
        {
            case GPUBuffer buffer:
                currentSRVs[slot] = GetSRVHandle(buffer).Ptr;
                break;
            case Texture2D texture:
                currentSRVs[slot] = GetSRVHandle(texture).Ptr;
                break;
        }
    }
    public void SetSRVTLim(int slot, Texture2D texture, int mips) => currentSRVs[slot] = GetSRVHandleWithMip(texture, mips).Ptr;

    void SetSRVRSlot(int slot, ulong gpuAddr) => currentSRVs[slot] = gpuAddr;

    public void SetCBVRSlot(int slot, CBuffer buffer) => currentCBVs[slot] = buffer.GetCurrentVirtualAddress();

    public void SetCBVRSlot<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        GetRingBuffer().UploadBuffer(data, out ulong addr);
        currentCBVs[slot] = addr;
    }

    public void SetRTSlot(int slot, Texture2D texture2D) => currentUAVs[slot] = GetUAVHandle(texture2D, ResourceStates.NonPixelShaderResource).Ptr;

    public void SetUAVTSlot(int slot, IGPUResource resource)
    {
        switch (resource)
        {
            case GPUBuffer buffer:
                currentUAVs[slot] = GetUAVHandle(buffer).Ptr;
                break;
            case Texture2D texture:
                currentUAVs[slot] = GetUAVHandle(texture).Ptr;
                break;
        }
    }

    public void SetUAVTSlot(int slot, Texture2D texture, int mipIndex)
    {
        texture.SetPartResourceState(m_commandList, ResourceStates.UnorderedAccess, mipIndex, 1);
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

        currentUAVs[slot] = CreateUAV(texture.resource, uavDesc).Ptr;
    }

    public void UploadMesh(Mesh mesh)
    {
        foreach (var vtBuf in mesh.vtBuffers)
        {
            var mesh1 = vtBuf.Value;
            int dataLength = mesh1.data.Length;
            int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.Capacity >= dataLength && u.Capacity <= dataLength * 2 + 256);
            if (index1 != -1)
            {
                mesh1.vertex = mesh.vtBuffersDisposed[index1].vertex;
                mesh1.Capacity = mesh.vtBuffersDisposed[index1].Capacity;
                m_commandList.ResourceBarrierTransition(mesh1.vertex, ResourceStates.Common, ResourceStates.CopyDest);
                GetRingBuffer().UploadTo<byte>(m_commandList, mesh1.data, mesh1.vertex);
                m_commandList.ResourceBarrierTransition(mesh1.vertex, ResourceStates.CopyDest, ResourceStates.Common);

                mesh.vtBuffersDisposed.RemoveAt(index1);
            }
            else
            {
                mesh1.Capacity = dataLength + 256;
                CreateBuffer(mesh1.Capacity, ref mesh1.vertex, ResourceStates.Common);
                GetRingBuffer().UploadTo<byte>(m_copyCommandList, mesh1.data, mesh1.vertex);
            }

            mesh1.vertex.Name = "vertex buffer" + vtBuf.Key;

            Reference(mesh1.vertex);

            mesh1.vertexBufferView.BufferLocation = mesh1.vertex.GPUVirtualAddress;
            mesh1.vertexBufferView.StrideInBytes = mesh1.stride;
            mesh1.vertexBufferView.SizeInBytes = dataLength;
        }

        foreach (var vtBuf in mesh.vtBuffersDisposed)
            vtBuf.vertex?.Release();
        mesh.vtBuffersDisposed.Clear();

        if (mesh.m_indexCount > 0)
        {
            int indexBufferLength = mesh.m_indexCount * 4;
            ref var indexBuffer = ref mesh.indexBuffer;
            if (mesh.indexBufferCapacity >= indexBufferLength)
            {
                m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.Common, ResourceStates.CopyDest);
                GetRingBuffer().UploadTo<byte>(m_commandList, new Span<byte>(mesh.m_indexData, 0, indexBufferLength), indexBuffer);
                m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.CopyDest, ResourceStates.Common);
            }
            else
            {
                CreateBuffer(indexBufferLength, ref indexBuffer, ResourceStates.Common);
                mesh.indexBufferCapacity = indexBufferLength;
                indexBuffer.Name = "index buffer";
                GetRingBuffer().UploadTo<byte>(m_copyCommandList, new Span<byte>(mesh.m_indexData, 0, indexBufferLength), indexBuffer);
            }
            Reference(indexBuffer);
            mesh.indexBufferView = new IndexBufferView
            {
                BufferLocation = indexBuffer.GPUVirtualAddress,
                SizeInBytes = indexBufferLength,
                Format = Format.R32_UInt
            };
        }
    }

    public void UpdateMeshOneFrame(Mesh mesh)
    {
        foreach (var vtBuf in mesh.vtBuffers)
        {
            var mesh1 = vtBuf.Value;
            int dataLength = mesh1.data.Length;

            GetRingBuffer().UploadBuffer<byte>(mesh1.data, out ulong addr);

            mesh1.vertexBufferView.BufferLocation = addr;
            mesh1.vertexBufferView.StrideInBytes = mesh1.stride;
            mesh1.vertexBufferView.SizeInBytes = dataLength;
        }

        foreach (var vtBuf in mesh.vtBuffersDisposed)
            vtBuf.vertex?.Release();
        mesh.vtBuffersDisposed.Clear();

        if (mesh.m_indexCount > 0)
        {
            int indexBufferLength = mesh.m_indexCount * 4;
            GetRingBuffer().UploadBuffer<byte>(new ReadOnlySpan<byte>(mesh.m_indexData, 0, indexBufferLength), out ulong addr);

            mesh.indexBufferView = new IndexBufferView
            {
                BufferLocation = addr,
                SizeInBytes = indexBufferLength,
                Format = Format.R32_UInt
            };
        }
    }

    public void UpdateMeshOneFrame<T>(Mesh mesh, ReadOnlySpan<T> data, string slot) where T : unmanaged
    {
        int size1 = Marshal.SizeOf<T>();
        int sizeInBytes = data.Length * size1;

        if (!mesh.vtBuffers.TryGetValue(slot, out var vtBuf))
        {
            vtBuf = mesh.AddBuffer(slot);
        }
        GetRingBuffer().UploadBuffer(data, out ulong addr);

        vtBuf.vertexBufferView.BufferLocation = addr;
        vtBuf.vertexBufferView.StrideInBytes = sizeInBytes / mesh.m_vertexCount;
        vtBuf.vertexBufferView.SizeInBytes = sizeInBytes;
    }

    public void EndUpdateMesh(Mesh mesh)
    {
        foreach (var vtBuf in mesh.vtBuffersDisposed)
            vtBuf.vertex?.Release();
        mesh.vtBuffersDisposed.Clear();
    }

    public void UpdateResource<T>(CBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        buffer.size = Marshal.SizeOf<T>() * data.Length;
        GetRingBuffer().UploadBuffer(data, out buffer.gpuRefAddress);
    }

    public void UpdateResource<T>(GPUBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        buffer.size = Marshal.SizeOf<T>() * data.Length;
        GetRingBuffer().UploadTo(m_commandList, data, buffer.resource);
    }

    public void UploadTexture(Texture2D texture, Uploader uploader)
    {
        texture.width = uploader.m_width;
        texture.height = uploader.m_height;
        texture.mipLevels = uploader.m_mipLevels;
        texture.format = uploader.m_format;

        var textureDesc = texture.GetResourceDescription();

        CreateResource(textureDesc, null, ref texture.resource);
        texture.InitResourceState(ResourceStates.CopyDest);
        texture.resource.Name = texture.Name ?? "tex2d";

        UploadTexture(texture, uploader.m_data);

        texture.Status = GraphicsObjectStatus.loaded;
    }

    void UploadTexture(Texture2D texture, byte[] data)
    {
        int bitsPerPixel = (int)BitsPerPixel(texture.format);
        int width = texture.width;
        int height = texture.height;

        int total = GetTotalBytes(width, height, texture.mipLevels, bitsPerPixel);

        ID3D12Resource uploadBuffer = null;
        CreateBuffer(total, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
        uploadBuffer.Name = "uploadbuffer tex";
        ResourceDelayRecycle(uploadBuffer);

        Span<SubresourceData> subresources = stackalloc SubresourceData[texture.mipLevels];
        IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
        for (int i = 0; i < texture.mipLevels; i++)
        {
            SubresourceData subresourcedata = new SubresourceData();
            int rowNumByte = width * bitsPerPixel / 8;
            subresourcedata.Data = pdata;
            subresourcedata.RowPitch = (IntPtr)rowNumByte;
            subresourcedata.SlicePitch = (IntPtr)(rowNumByte * height);
            pdata += rowNumByte * height;

            subresources[i] = subresourcedata;
            width /= 2;
            height /= 2;
        }
        texture.SetAllResourceState(m_commandList, ResourceStates.CopyDest);
        UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, texture.mipLevels, subresources);
        texture.SetAllResourceState(m_commandList, ResourceStates.GenericRead);
        Reference(texture.resource);
    }

    int GetTotalBytes(int width, int height, int mipLevels, int bitsPerPixel)
    {
        int total = 0;
        for (int i = 0; i < mipLevels; i++)
        {
            int rowNumByte1 = (width * bitsPerPixel / 8 + 255) & ~255;
            total += rowNumByte1 * height;
            width /= 2;
            height /= 2;
        }
        return total;
    }

    public void UpdateRenderTexture(Texture2D texture)
    {
        var textureDesc = texture.GetResourceDescription();

        ClearValue clearValue = texture.dsvFormat != Format.Unknown
            ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
            : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
        CreateResource(textureDesc, clearValue, ref texture.resource, ResourceStates.GenericRead);
        texture.InitResourceState(ResourceStates.GenericRead);
        texture.resource.Name = texture.Name ?? "render tex2D";

        texture.Status = GraphicsObjectStatus.loaded;
    }

    public void UpdateDynamicBuffer(GPUBuffer buffer)
    {
        CreateUAVBuffer(buffer.size, ref buffer.resource);
        if (buffer.Name != null)
            buffer.resource.Name = buffer.Name;
        buffer.resourceStates = ResourceStates.UnorderedAccess;
    }

    public void UpdateReadBackBuffer(ReadBackBuffer buffer, int size)
    {
        CreateBuffer(size, ref buffer.bufferReadBack, heapType: HeapType.Readback);
        buffer.size = size;
        buffer.bufferReadBack.Name = "texture readback";
    }

    void ClearMeshState()
    {
        currentMesh.Clear();
        meshChanged = true;
    }

    public void SetMesh(Mesh mesh)
    {
        ClearMeshState();
        m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        SetMesh1(mesh);
    }

    void SetMesh1(Mesh mesh)
    {
        if (mesh.baseMesh != null)
        {
            SetMesh1(mesh.baseMesh);
        }
        foreach (var vtBuf in mesh.vtBuffers)
        {
            currentMesh[vtBuf.Key] = vtBuf.Value.vertexBufferView;
            if (vtBuf.Value.vertex != null)
            {
                Reference(vtBuf.Value.vertex);
            }
        }
        if (mesh.indexBuffer != null)
        {
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            Reference(mesh.indexBuffer);
        }
    }

    public void SetMesh(ReadOnlySpan<byte> vertexData, ReadOnlySpan<byte> indexData, int vertexCount, int indexCount)
    {
        ClearMeshState();
        m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        if (vertexData != null)
        {
            GetRingBuffer().UploadBuffer(vertexData, out ulong vertexGpuAddress);
            VertexBufferView vertexBufferView;
            vertexBufferView.BufferLocation = vertexGpuAddress;
            vertexBufferView.SizeInBytes = vertexData.Length;
            vertexBufferView.StrideInBytes = vertexData.Length / vertexCount;
            m_commandList.IASetVertexBuffers(0, vertexBufferView);
        }

        if (indexData != null)
        {
            GetRingBuffer().UploadBuffer(indexData, out ulong indexGpuAddress);
            IndexBufferView indexBufferView = new IndexBufferView
            {
                BufferLocation = indexGpuAddress,
                SizeInBytes = indexData.Length,
                Format = (indexData.Length / indexCount) == 2 ? Format.R16_UInt : Format.R32_UInt
            };
            m_commandList.IASetIndexBuffer(indexBufferView);
        }
    }

    public void CopyTexture(Texture2D target, Texture2D source)
    {
        target.SetAllResourceState(m_commandList, ResourceStates.CopyDest);
        source.SetAllResourceState(m_commandList, ResourceStates.GenericRead);
        m_commandList.CopyResource(target.resource, source.resource);
    }

    public int ReadBack(ReadBackBuffer target, Texture2D texture2D)
    {
        var source = texture2D.resource;
        texture2D.SetAllResourceState(m_commandList, ResourceStates.CopySource);
        int RowPitch = (texture2D.width * 4 + 255) & ~255;
        int offset = target.GetOffsetAndMove(RowPitch * texture2D.height);
        PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
        footPrint.Footprint.Width = texture2D.width;
        footPrint.Footprint.Height = texture2D.height;
        footPrint.Footprint.Depth = 1;
        footPrint.Footprint.RowPitch = RowPitch;
        footPrint.Footprint.Format = texture2D.format;
        footPrint.Offset = (ulong)offset;

        TextureCopyLocation Dst = new TextureCopyLocation(target.bufferReadBack, footPrint);
        TextureCopyLocation Src = new TextureCopyLocation(source, 0);
        m_commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);

        return offset;
    }

    public void RSSetScissorRect(int left, int top, int right, int bottom)
    {
        m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
    }
    public void RSSetScissorRectAndViewport(int left, int top, int right, int bottom)
    {
        m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
        m_commandList.RSSetViewport(left, top, right - left, bottom - top);
    }

    public void Begin()
    {
        m_commandList = graphicsDevice.commandQueue.GetCommandList();
        m_commandList.Reset(graphicsDevice.commandQueue.GetCommandAllocator());
        m_commandList.SetDescriptorHeaps(graphicsDevice.cbvsrvuavHeap.heap);
        m_copyCommandList = graphicsDevice.copyCommandQueue.GetCommandList();
        m_copyCommandList.Reset(graphicsDevice.copyCommandQueue.GetCommandAllocator());
        ClearState();
        TriangleCount = 0;
    }

    public void ClearState()
    {
        ClearMeshState();
        currentPSO = null;
        currentRTPSO = null;
        _currentGraphicsRootSignature = null;
        _currentComputeRootSignature = null;
        currentRootSignature = null;
        currentCBVs.Clear();
        currentSRVs.Clear();
        currentUAVs.Clear();
    }

    public void ClearSwapChain(SwapChain swapChain, Vector4 color)
    {
        var handle1 = graphicsDevice.GetRenderTargetView(swapChain.GetResource(m_commandList));
        m_commandList.ClearRenderTargetView(handle1, new Vortice.Mathematics.Color(color));
        presents.Add(swapChain);
    }

    public void ClearDSV(Texture2D texture)
    {
        Reference(texture.resource);
        var dsv = graphicsDevice.GetDepthStencilView(texture.resource);
        m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
    }

    public void ClearRTV(Texture2D texture, Vector4 color)
    {
        Reference(texture.resource);
        var rtv = graphicsDevice.GetRenderTargetView(texture.resource);
        m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
    }

    public void SetRTV(Texture2D RTV, Vector4 color, bool clear) => SetRTVDSV(RTV, null, color, clear, false);

    public void SetRTV(ReadOnlySpan<Texture2D> RTVs, Vector4 color, bool clear) => SetRTVDSV(RTVs, null, color, clear, false);

    public void SetDSV(Texture2D texture, bool clear)
    {
        SetViewportAndRect(texture);
        texture.SetAllResourceState(m_commandList, ResourceStates.DepthWrite);
        var dsv = graphicsDevice.GetDepthStencilView(texture.resource);
        Reference(texture.resource);
        if (clear)
            m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
        m_commandList.OMSetRenderTargets(Array.Empty<CpuDescriptorHandle>(), dsv);
    }

    public void SetRTVDSV(Texture2D RTV, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
    {
        SetViewportAndRect(RTV);
        RTV.SetAllResourceState(m_commandList, ResourceStates.RenderTarget);
        var rtv = graphicsDevice.GetRenderTargetView(RTV.resource);
        Reference(RTV.resource);
        if (clearRTV)
            m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
        if (DSV != null)
        {
            DSV.SetAllResourceState(m_commandList, ResourceStates.DepthWrite);
            var dsv = graphicsDevice.GetDepthStencilView(DSV.resource);
            Reference(DSV.resource);
            if (clearDSV)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(rtv, dsv);
        }
        else
        {
            m_commandList.OMSetRenderTargets(rtv);
        }
    }

    public void SetRTVDSV(ReadOnlySpan<Texture2D> RTVs, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
    {
        SetViewportAndRect(RTVs[0]);

        Span<CpuDescriptorHandle> handles = stackalloc CpuDescriptorHandle[RTVs.Length];
        for (int i = 0; i < RTVs.Length; i++)
        {
            RTVs[i].SetAllResourceState(m_commandList, ResourceStates.RenderTarget);
            handles[i] = graphicsDevice.GetRenderTargetView(RTVs[i].resource);
            Reference(RTVs[i].resource);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
        }
        if (DSV != null)
        {
            DSV.SetAllResourceState(m_commandList, ResourceStates.DepthWrite);
            var dsv = graphicsDevice.GetDepthStencilView(DSV.resource);
            Reference(DSV.resource);
            if (clearDSV)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);

            m_commandList.OMSetRenderTargets(handles, dsv);
        }
        else
        {
            m_commandList.OMSetRenderTargets(handles);
        }
    }

    void SetViewportAndRect(Texture2D texture)
    {
        m_commandList.RSSetScissorRect(texture.width, texture.height);
        m_commandList.RSSetViewport(0, 0, texture.width, texture.height);
    }

    public void ClearTexture(Texture2D texture, Vector4 color, float depth)
    {
        switch (texture.GetFormat())
        {
            case Format.D16_UNorm:
            case Format.D24_UNorm_S8_UInt:
            case Format.D32_Float:
            case Format.D32_Float_S8X24_UInt:
                texture.SetAllResourceState(m_commandList, ResourceStates.DepthWrite);
                for (int i = 0; i < texture.mipLevels; i++)
                {
                    var dsv = graphicsDevice.GetDepthStencilView(texture.resource, i);
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, depth, 0);
                }
                break;
            case Format.Unknown:
                break;
            default:
                texture.SetAllResourceState(m_commandList, ResourceStates.RenderTarget);
                for (int i = 0; i < texture.mipLevels; i++)
                {
                    var rtv = graphicsDevice.GetRenderTargetView(texture.resource, i);
                    m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
                }
                break;
        }
        Reference(texture.resource);
    }

    internal void SetRootSignature(RootSignature rootSignature)
    {
        ClearState();
        this.currentRootSignature = rootSignature;
        rootSignature.GetRootSignature(graphicsDevice);
    }

    public void SetRenderTargetSwapChain(SwapChain swapChain, Vector4 color, bool clear)
    {
        m_commandList.RSSetScissorRect(swapChain.width, swapChain.height);
        m_commandList.RSSetViewport(0, 0, swapChain.width, swapChain.height);
        var renderTargetView = graphicsDevice.GetRenderTargetView(swapChain.GetResource(m_commandList));
        if (clear)
            m_commandList.ClearRenderTargetView(renderTargetView, new Vortice.Mathematics.Color4(color));
        m_commandList.OMSetRenderTargets(renderTargetView);
        presents.Add(swapChain);
    }

    public void _DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
    {
        m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        TriangleCount += indexCountPerInstance / 3 * instanceCount;
    }

    public void Draw(int vertexCount, int startVertexLocation)
    {
        PipelineBinding();
        m_commandList.DrawInstanced(vertexCount, 1, startVertexLocation, 0);
        TriangleCount += vertexCount / 3;
    }

    public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
    {
        DrawIndexedInstanced(indexCount, 1, startIndexLocation, baseVertexLocation, 0);
    }

    public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
    {
        PipelineBinding();
        m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        TriangleCount += indexCountPerInstance / 3 * instanceCount;
    }

    void PipelineBindingCompute()
    {
        if (_currentComputeRootSignature != currentRootSignature)
        {
            _currentComputeRootSignature = currentRootSignature;
            _currentGraphicsRootSignature = null;
            m_commandList.SetComputeRootSignature(currentRootSignature.rootSignature);
        }
        var description1 = currentRootSignature.description1.Parameters;
        for (int i = 0; i < description1.Length; i++)
        {
            var d = description1[i];
            if (d.ParameterType == RootParameterType.ConstantBufferView)
            {
                if (currentCBVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetComputeRootConstantBufferView(i, addr);
            }
            else if (d.ParameterType == RootParameterType.ShaderResourceView)
            {
                if (currentSRVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetComputeRootShaderResourceView(i, addr);
            }
            else if (d.ParameterType == RootParameterType.UnorderedAccessView)
            {
                if (currentUAVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetComputeRootUnorderedAccessView(i, addr);
            }
            else
            {
                var rangeType = d.DescriptorTable.Ranges[0].RangeType;
                int register = d.DescriptorTable.Ranges[0].BaseShaderRegister;
                if (rangeType == DescriptorRangeType.ConstantBufferView)
                {
                    if (currentCBVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
                else if (rangeType == DescriptorRangeType.ShaderResourceView)
                {
                    if (currentSRVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
                else if (rangeType == DescriptorRangeType.UnorderedAccessView)
                {
                    if (currentUAVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
            }
        }
    }

    void PipelineBinding()
    {
        if (_currentGraphicsRootSignature != currentRootSignature)
        {
            _currentGraphicsRootSignature = currentRootSignature;
            _currentComputeRootSignature = null;
            m_commandList.SetGraphicsRootSignature(currentRootSignature.rootSignature);
        }
        if (meshChanged)
        {
            var elements = currentPSO.inputLayoutDescription.Elements;
            for (int i = 0; i < elements.Length; i++)
            {
                InputElementDescription element = elements[i];
                if (currentMesh.TryGetValue(element.SemanticName + element.SemanticIndex, out var mesh))
                {
                    m_commandList.IASetVertexBuffers(i, mesh);
                }
            }
            meshChanged = false;
        }

        var description1 = currentRootSignature.description1.Parameters;
        for (int i = 0; i < description1.Length; i++)
        {
            var d = description1[i];
            if (d.ParameterType == RootParameterType.ConstantBufferView)
            {
                if (currentCBVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetGraphicsRootConstantBufferView(i, addr);
            }
            else if (d.ParameterType == RootParameterType.ShaderResourceView)
            {
                if (currentSRVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetGraphicsRootShaderResourceView(i, addr);
            }
            else if (d.ParameterType == RootParameterType.UnorderedAccessView)
            {
                if (currentUAVs.TryGetValue(d.Descriptor.ShaderRegister, out ulong addr))
                    m_commandList.SetGraphicsRootUnorderedAccessView(i, addr);
            }
            else
            {
                var rangeType = d.DescriptorTable.Ranges[0].RangeType;
                int register = d.DescriptorTable.Ranges[0].BaseShaderRegister;
                if (rangeType == DescriptorRangeType.ConstantBufferView)
                {
                    if (currentCBVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
                else if (rangeType == DescriptorRangeType.ShaderResourceView)
                {
                    if (currentSRVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
                else if (rangeType == DescriptorRangeType.UnorderedAccessView)
                {
                    if (currentUAVs.TryGetValue(register, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                }
            }
        }

        //if (psoChange)
        //{
        //    psoChange = false;

        //    int variantIndex = currentPSO.GetVariantIndex(graphicsDevice, currentRootSignature, currentPSODesc);
        //    if (variantIndex != -1)
        //    {
        //        m_commandList.SetPipelineState(currentPSO.m_pipelineStates[variantIndex]);
        //        Reference(currentPSO.m_pipelineStates[variantIndex]);
        //    }
        //}
    }

    public void Dispatch(int x, int y, int z)
    {
        PipelineBindingCompute();
        m_commandList.Dispatch(x, y, z);
    }

    public void Execute()
    {
        foreach (var val in presents)
        {
            val.EndRenderTarget(m_commandList);
        }
        presents.Clear();

        graphicsDevice.superRingBuffer.DelayCommands(m_copyCommandList);
        m_copyCommandList.Close();
        graphicsDevice.copyCommandQueue.ExecuteCommandList(m_copyCommandList);
        graphicsDevice.copyCommandQueue.NextExecuteIndex();
        m_commandList.Close();
        graphicsDevice.commandQueue.WaitFor(graphicsDevice.copyCommandQueue);

        graphicsDevice.commandQueue.ExecuteCommandList(m_commandList);
        m_commandList = null;

        foreach (var resource in referenceThisCommand)
        {
            graphicsDevice.commandQueue.ResourceDelayRecycle(resource);
        }
        referenceThisCommand.Clear();
        // 提高帧索引。
        graphicsDevice.commandQueue.NextExecuteIndex();
    }

    void CreateBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.CopyDest, HeapType heapType = HeapType.Default)
    {
        ResourceDelayRecycle(resource);
        ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
            new HeapProperties(heapType),
            HeapFlags.None,
            ResourceDescription.Buffer((ulong)bufferLength),
            resourceStates,
            null,
            out resource));
    }
    void CreateUAVBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.UnorderedAccess)
    {
        ResourceDelayRecycle(resource);
        ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
            new HeapProperties(HeapType.Default),
            HeapFlags.None,
            ResourceDescription.Buffer((ulong)bufferLength, ResourceFlags.AllowUnorderedAccess),
            resourceStates,
            null,
            out resource));
    }

    void CreateResource(ResourceDescription resourceDescription, ClearValue? clearValue, ref ID3D12Resource resource,
        ResourceStates resourceStates = ResourceStates.CopyDest, HeapType heapType = HeapType.Default)
    {
        ResourceDelayRecycle(resource);
        ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
            new HeapProperties(heapType),
            HeapFlags.None,
            resourceDescription,
            resourceStates,
            clearValue,
            out resource));
    }

    void _RTWriteGpuAddr<T>(ReadOnlySpan<T> data, BinaryWriter writer) where T : unmanaged
    {
        GetRingBuffer().UploadBuffer(data, out ulong addr);
        writer.Write(addr);
    }

    GpuDescriptorHandle GetUAVHandle(Texture2D texture, ResourceStates state = ResourceStates.UnorderedAccess)
    {
        texture.SetAllResourceState(m_commandList, state);
        if (texture.isCube)
        {
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = texture.uavFormat,
                ViewDimension = UnorderedAccessViewDimension.Texture2DArray,
            };
            uavDesc.Texture2DArray.ArraySize = 6;
            return CreateUAV(texture.resource, uavDesc);
        }
        else
        {
            return CreateUAV(texture.resource, null);
        }
    }

    GpuDescriptorHandle GetUAVHandle(GPUBuffer buffer)
    {
        buffer.StateChange(m_commandList, ResourceStates.UnorderedAccess);
        var uavDesc = new UnorderedAccessViewDescription()
        {
            Format = Format.R32_Typeless,
            ViewDimension = UnorderedAccessViewDimension.Buffer,
            Buffer = new BufferUnorderedAccessView()
            {
                Flags = BufferUnorderedAccessViewFlags.Raw,
                NumElements = buffer.size / 4
            }
        };

        return CreateUAV(buffer.resource, uavDesc);
    }
    GpuDescriptorHandle GetSRVHandle(GPUBuffer buffer)
    {
        buffer.StateChange(m_commandList, ResourceStates.GenericRead);
        ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription()
        {
            Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
            Format = Format.R32_Typeless,
            ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer
        };
        srvDesc.Buffer.FirstElement = 0;
        srvDesc.Buffer.NumElements = buffer.size / 4;
        srvDesc.Buffer.Flags = BufferShaderResourceViewFlags.Raw;

        return CreateSRV(buffer.resource, srvDesc);
    }
    GpuDescriptorHandle GetSRVHandle(Texture2D texture, bool linear = false)
    {
        texture.SetAllResourceState(m_commandList, ResourceStates.GenericRead);

        var format = texture.format;
        ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription
        {
            Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
            Format = (linear && format == Format.R8G8B8A8_UNorm_SRgb) ? Format.R8G8B8A8_UNorm : format
        };
        if (texture.isCube)
        {
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = texture.mipLevels;
        }
        else
        {
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = texture.mipLevels;
        }

        return CreateSRV(texture.resource, srvDesc);
    }

    GpuDescriptorHandle GetSRVHandleWithMip(Texture2D texture, int mips)
    {
        texture.SetPartResourceState(m_commandList, ResourceStates.GenericRead, mips, 1);
        ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription
        {
            Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
            Format = texture.format,
        };
        if (texture.isCube)
        {
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = 1;
            srvDesc.TextureCube.MostDetailedMip = mips;
        }
        else
        {
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D.MipLevels = 1;
            srvDesc.Texture2D.MostDetailedMip = mips;
        }

        return CreateSRV(texture.resource, srvDesc);
    }

    GpuDescriptorHandle CreateSRV(ID3D12Resource resource, ShaderResourceViewDescription srvDesc)
    {
        graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
        graphicsDevice.device.CreateShaderResourceView(resource, srvDesc, cpuHandle);
        Reference(resource);
        return gpuHandle;
    }

    GpuDescriptorHandle CreateUAV(ID3D12Resource resource, UnorderedAccessViewDescription? uavDesc)
    {
        graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
        graphicsDevice.device.CreateUnorderedAccessView(resource, null, uavDesc, cpuHandle);
        Reference(resource);
        return gpuHandle;
    }

    void ResourceDelayRecycle(ID3D12Object iD3D12Object)
    {
        if (iD3D12Object == null)
            return;
        if (!referenceThisCommand.Add(iD3D12Object))
        {
            iD3D12Object.Release();
        }
    }

    void Reference(ID3D12Object iD3D12Object)
    {
        if (referenceThisCommand.Add(iD3D12Object))
            iD3D12Object.AddRef();
    }
    ulong ReferenceGetAddr(ID3D12Resource iD3D12Object)
    {
        if (referenceThisCommand.Add(iD3D12Object))
            iD3D12Object.AddRef();
        return iD3D12Object.GPUVirtualAddress;
    }
    public HashSet<ID3D12Object> referenceThisCommand = new HashSet<ID3D12Object>();

    HashSet<SwapChain> presents = new HashSet<SwapChain>();

    RingBuffer GetRingBuffer() => graphicsDevice.superRingBuffer;
}
