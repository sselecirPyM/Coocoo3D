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
    GraphicsDevice graphicsDevice;
    internal ID3D12GraphicsCommandList4 m_commandList;
    ID3D12GraphicsCommandList4 m_copyCommandList;
    internal RootSignature currentRootSignature;
    RootSignature _currentGraphicsRootSignature;
    RootSignature _currentComputeRootSignature;

    public Dictionary<int, object> slots = new Dictionary<int, object>();

    public RTPSO currentRTPSO;
    public PSO currentPSO;

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
        Reference(pipelineState);
        Reference(rootSignature.rootSignature);
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

    public void BuildAccelerationStruct(RTTopLevelAcclerationStruct accelerationStruct)
    {
        //int scratchResourceSize = 134217728;
        int scratchResourceSize = 16777216;
        if (graphicsDevice.scratchResource == null)
        {
            CreateUAVBuffer(scratchResourceSize, ref graphicsDevice.scratchResource, ResourceStates.UnorderedAccess);
        }
        int bufferSize = 0;
        var instances = accelerationStruct.instances;
        var blasInputs = new List<(BuildRaytracingAccelerationStructureInputs, RaytracingAccelerationStructurePrebuildInfo)>();


        foreach (var instance in instances)
        {
            var inputs = GetBLASInputs(instance.blas);
            var info = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);
            info.ScratchDataSizeInBytes = (info.ScratchDataSizeInBytes + 255) & ~255uL;
            info.ResultDataMaxSizeInBytes = (info.ResultDataMaxSizeInBytes + 255) & ~255uL;
            bufferSize += (int)info.ResultDataMaxSizeInBytes;
            blasInputs.Add((inputs, info));
        }
        var tlInputs = new BuildRaytracingAccelerationStructureInputs
        {
            Type = RaytracingAccelerationStructureType.TopLevel,
            Layout = ElementsLayout.Array,
            DescriptorsCount = instances.Count,
        };
        var tlasInfo = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(tlInputs);
        bufferSize += (int)((tlasInfo.ResultDataMaxSizeInBytes + 255) & ~255uL);
        ID3D12Resource resource = null;
        CreateUAVBuffer((int)bufferSize, ref resource, ResourceStates.RaytracingAccelerationStructure);
        Reference(resource);
        resource.Release();

        ulong startAddress = resource.GPUVirtualAddress;
        ulong scratchSizeUsed = 0;
        for (int i = 0; i < instances.Count; i++)
        {
            RTInstance instance = instances[i];
            var inputs = blasInputs[i];
            if (scratchSizeUsed + inputs.Item2.ScratchDataSizeInBytes > (ulong)scratchResourceSize)
            {
                scratchSizeUsed = 0;
                m_commandList.ResourceBarrierUnorderedAccessView(resource);
            }

            instance.blas.GPUVirtualAddress = startAddress;
            BuildAS(inputs.Item1, graphicsDevice.scratchResource.GPUVirtualAddress + scratchSizeUsed, instance.blas.GPUVirtualAddress);
            startAddress += inputs.Item2.ResultDataMaxSizeInBytes;
            scratchSizeUsed += inputs.Item2.ScratchDataSizeInBytes;
        }
        m_commandList.ResourceBarrierUnorderedAccessView(resource);


        Span<RaytracingInstanceDescription> raytracingInstanceDescriptions = stackalloc RaytracingInstanceDescription[instances.Count];
        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            var btas = instance.blas;
            int instantID = i;
            raytracingInstanceDescriptions[i] = new RaytracingInstanceDescription
            {
                AccelerationStructure = btas.GPUVirtualAddress,
                InstanceContributionToHitGroupIndex = (Vortice.UInt24)(uint)instantID,
                InstanceID = (Vortice.UInt24)(uint)instantID,
                InstanceMask = instance.instanceMask,
                Transform = GetMatrix3X4(Matrix4x4.Transpose(instance.transform))
            };
        }
        readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(raytracingInstanceDescriptions), 64, out ulong gpuInstDesc);

        accelerationStruct.GPUVirtualAddress = startAddress;
        tlInputs.InstanceDescriptions = gpuInstDesc;
        BuildAS(tlInputs, graphicsDevice.scratchResource.GPUVirtualAddress, accelerationStruct.GPUVirtualAddress);
    }

    BuildRaytracingAccelerationStructureInputs GetBLASInputs(RTBottomLevelAccelerationStruct blas)
    {
        string POSITION = "POSITION0";
        var mesh = blas.mesh;

        var indexBuffer = mesh.GetIndexBuffer();
        var positionBuffer = mesh.GetVertexBuffer(POSITION);

        ulong position = positionBuffer.vertexBufferView.BufferLocation + (ulong)blas.vertexStart * 12;


        if (positionBuffer.resource != null)
            Reference(positionBuffer.resource);
        Reference(indexBuffer);
        return new BuildRaytracingAccelerationStructureInputs
        {
            Type = RaytracingAccelerationStructureType.BottomLevel,
            Layout = ElementsLayout.Array,
            DescriptorsCount = 1,
            GeometryDescriptions = new RaytracingGeometryDescription[]
            {
                new RaytracingGeometryDescription(new RaytracingGeometryTrianglesDescription(
                    new GpuVirtualAddressAndStride(position, 12),
                    Format.R32G32B32_Float,
                    blas.vertexCount,
                    0,
                    indexBuffer.GPUVirtualAddress + (ulong)blas.indexStart * 4,
                    Format.R32_UInt,
                    blas.indexCount),RaytracingGeometryFlags.Opaque),
            }
        };
    }

    void BuildAS(BuildRaytracingAccelerationStructureInputs inputs, ulong scratchGPUVirtualAddress, ulong GPUVirtualAddress)
    {
        var brtas = new BuildRaytracingAccelerationStructureDescription
        {
            Inputs = inputs,
            ScratchAccelerationStructureData = scratchGPUVirtualAddress,
            DestinationAccelerationStructureData = GPUVirtualAddress
        };

        m_commandList.BuildRaytracingAccelerationStructure(brtas);
    }
    const int D3D12ShaderIdentifierSizeInBytes = 32;

    public void DispatchRays(int width, int height, int depth, RayTracingCall call)
    {
        var pRtsoProps = currentRTPSO.so.QueryInterface<ID3D12StateObjectProperties>();
        Reference(currentRTPSO.so);

        currentRootSignature = currentRTPSO.globalRootSignature;

        var dispatchRaysDescription = new DispatchRaysDescription
        {
            Width = width,
            Height = height,
            Depth = depth,
        };

        {
            readonlyBufferAllocator.Upload(GetShaderIdentifier(pRtsoProps, call.rayGenShader), 64, out var gpuaddr);
            dispatchRaysDescription.RayGenerationShaderRecord = new GpuVirtualAddressRange(gpuaddr, (ulong)D3D12ShaderIdentifierSizeInBytes);
        }

        MemoryStream memoryStream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(memoryStream);
        writer.Seek(0, SeekOrigin.Begin);

        var rtpso = currentRTPSO;
        var proxy = new LocalResourceProxy
        {
            graphicsContext = this,
            pRtsoProps = pRtsoProps,
            buffer = new byte[rtpso.localSize],
            srvs = rtpso.localSRV,
            cbvs = rtpso.localCBV
        };

        foreach (var inst in call.tpas.instances)
        {
            WriteLocalHandles(inst, proxy, writer);
            BufferAlign(writer, 64);
        }

        if (memoryStream.Position > 0)
        {
            int length1 = (int)memoryStream.Position;
            readonlyBufferAllocator.Upload(new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, length1), 64, out var gpuaddr);
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
            readonlyBufferAllocator.Upload(new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, length1), out var gpuaddr);
            dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, D3D12ShaderIdentifierSizeInBytes);
        }

        writer.Seek(0, SeekOrigin.Begin);
        pRtsoProps.Dispose();
        PipelineBindingCompute2();
        SetComputeResources(call.SetResources);
        //var computeResourceProxy = new ComputeResourceProxy()
        //{
        //    graphicsContext = this,
        //    cbvs = currentRootSignature.cbv,
        //    srvs = currentRootSignature.srv,
        //    uavs = currentRootSignature.uav,
        //};
        //call.SetResources(computeResourceProxy);
        m_commandList.DispatchRays(dispatchRaysDescription);
    }

    static unsafe ReadOnlySpan<byte> GetShaderIdentifier(ID3D12StateObjectProperties pRtsoProps, string shader)
    {
        return new ReadOnlySpan<byte>(pRtsoProps.GetShaderIdentifier(shader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
    }

    static void WriteLocalHandles(RTInstance inst, LocalResourceProxy proxy, BinaryWriter writer)
    {
        inst.SetLocalResource(proxy);
        writer.Write(proxy.buffer);
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

    public void SetSRVTSlot<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.Length == 0)
        {
            //currentSRVs[slot] = 0uL;
            currentSRVs.Remove(slot);
        }
        else
        {
            var handle = readonlyBufferAllocator.GetSRV(MemoryMarshal.AsBytes(data));
            currentSRVs[slot] = handle;
        }
    }

    public void SetSRVTSlot(int slot, Mesh mesh, string bufferName)
    {
        var buffer = mesh.GetVertexBuffer(bufferName);

        if (buffer.baseBuffer != null)
        {
            buffer.baseBuffer.ToState(m_commandList, ResourceStates.Common);

            var handle = CreateSRV(buffer.baseBuffer.resource, new ShaderResourceViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer,
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                Buffer = new BufferShaderResourceView
                {
                    FirstElement = (ulong)buffer.baseBufferOffset / 4,
                    NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                    Flags = BufferShaderResourceViewFlags.Raw,
                }
            });
            currentSRVs[slot] = handle;
        }
        else
        {
            var handle = CreateSRV(buffer.resource, new ShaderResourceViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer,
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                Buffer = new BufferShaderResourceView
                {
                    FirstElement = 0,
                    NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                    Flags = BufferShaderResourceViewFlags.Raw,
                }
            });
            currentSRVs[slot] = handle;
        }
    }

    public void SetSRVTMip(int slot, Texture2D texture, int mips) => currentSRVs[slot] = GetSRVHandleWithMip(texture, mips).Ptr;

    void SetSRVRSlot(int slot, ulong gpuAddr) => currentSRVs[slot] = gpuAddr;

    public void SetCBVRSlot<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), 256, out ulong addr);
        currentCBVs[slot] = addr;
    }

    public ulong UploadCBV<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), 256, out ulong addr);
        return addr;
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

    public void SetUAVTSlot(int slot, Mesh mesh, string bufferName)
    {
        var buffer = mesh.GetVertexBuffer(bufferName);
        buffer.baseBuffer.ToState(m_commandList, ResourceStates.UnorderedAccess);

        var handle = CreateUAV(buffer.baseBuffer.resource, new UnorderedAccessViewDescription()
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
        currentUAVs[slot] = handle;
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
            var vertexBuffer = vtBuf.Value;
            int dataLength = vertexBuffer.data.Length;

            vertexBuffer.Capacity = dataLength + 256;
            CreateBuffer(vertexBuffer.Capacity, ref vertexBuffer.resource, ResourceStates.Common);
            GetRingBuffer().UploadTo(m_copyCommandList, vertexBuffer.data, vertexBuffer.resource);

            vertexBuffer.resource.Name = "vertex buffer" + vtBuf.Key;

            Reference(vertexBuffer.resource);

            vertexBuffer.vertexBufferView.BufferLocation = vertexBuffer.resource.GPUVirtualAddress;
            vertexBuffer.vertexBufferView.StrideInBytes = vertexBuffer.stride;
            vertexBuffer.vertexBufferView.SizeInBytes = dataLength;
        }

        foreach (var vtBuf in mesh.vtBuffersDisposed)
            vtBuf.resource?.Release();
        mesh.vtBuffersDisposed.Clear();

        if (mesh.m_indexCount > 0)
        {
            int indexBufferLength = mesh.m_indexCount * 4;
            ref var indexBuffer = ref mesh.indexBuffer;
            if (mesh.indexBufferCapacity >= indexBufferLength)
            {
                m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.Common, ResourceStates.CopyDest);
                GetRingBuffer().UploadTo(m_commandList, new Span<byte>(mesh.m_indexData, 0, indexBufferLength), indexBuffer);
                m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.CopyDest, ResourceStates.Common);
            }
            else
            {
                CreateBuffer(indexBufferLength, ref indexBuffer, ResourceStates.Common);
                mesh.indexBufferCapacity = indexBufferLength;
                indexBuffer.Name = "index buffer";
                GetRingBuffer().UploadTo(m_copyCommandList, new Span<byte>(mesh.m_indexData, 0, indexBufferLength), indexBuffer);
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

    public void UpdateMeshOneFrame<T>(Mesh mesh, ReadOnlySpan<T> data, string slot) where T : unmanaged
    {
        var data1 = MemoryMarshal.AsBytes(data);

        if (!mesh.vtBuffers.TryGetValue(slot, out var vtBuf))
        {
            vtBuf = mesh.AddBuffer(slot);
        }
        graphicsDevice.fastBufferAllocatorUAV.Upload(data1, out var addr, out vtBuf.baseBuffer, out vtBuf.baseBufferOffset);

        vtBuf.vertexBufferView.BufferLocation = addr;
        vtBuf.vertexBufferView.StrideInBytes = data1.Length / mesh.m_vertexCount;
        vtBuf.vertexBufferView.SizeInBytes = data1.Length;
    }

    public void CopyBaseMesh(Mesh mesh, string bufferName)
    {
        if (!mesh.vtBuffers.TryGetValue(bufferName, out var vtBuf))
        {
            vtBuf = mesh.AddBuffer(bufferName);
        }
        if (mesh.baseMesh.vtBuffers.TryGetValue(bufferName, out var baseBuffer1))
        {

        }

        graphicsDevice.fastBufferAllocatorUAV.GetCopy(m_commandList, baseBuffer1.resource, 0, baseBuffer1.vertexBufferView.SizeInBytes,
            out var addr, out vtBuf.baseBuffer, out vtBuf.baseBufferOffset);

        vtBuf.vertexBufferView.BufferLocation = addr;
        vtBuf.vertexBufferView.StrideInBytes = baseBuffer1.vertexBufferView.StrideInBytes;
        vtBuf.vertexBufferView.SizeInBytes = baseBuffer1.vertexBufferView.SizeInBytes;
    }

    public void EndUpdateMesh(Mesh mesh)
    {
        foreach (var vtBuf in mesh.vtBuffersDisposed)
            vtBuf.resource?.Release();
        mesh.vtBuffersDisposed.Clear();
    }

    public void UpdateResource<T>(GPUBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        var data1 = MemoryMarshal.AsBytes(data);
        buffer.size = data1.Length;
        GetRingBuffer().UploadTo(m_commandList, data1, buffer.resource);
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
            if (vtBuf.Value.resource != null)
            {
                Reference(vtBuf.Value.resource);
            }
            var baseBuffer = vtBuf.Value.baseBuffer;
            if (baseBuffer != null)
            {
                baseBuffer.ToState(m_commandList, ResourceStates.Common);
            }
        }
        if (mesh.indexBuffer != null)
        {
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            Reference(mesh.indexBuffer);
        }
    }

    public void SetSimpleMesh2(ReadOnlySpan<byte> vertexData, ReadOnlySpan<byte> indexData, int vertexCount, int indexCount)
    {
        int vertexStride = 0;
        int indexStride = 0;
        if (vertexData != null)
        {
            vertexStride = vertexData.Length / vertexCount;
        }
        if (indexData != null)
        {
            indexStride = indexData.Length / indexCount;
        }
        SetSimpleMesh(vertexData, indexData, vertexStride, indexStride);
    }

    public void SetSimpleMesh(ReadOnlySpan<byte> vertexData, ReadOnlySpan<byte> indexData, int vertexStride, int indexStride)
    {
        ClearMeshState();
        m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        if (vertexData != null)
        {
            readonlyBufferAllocator.Upload(vertexData, out ulong vertexGpuAddress);
            VertexBufferView vertexBufferView;
            vertexBufferView.BufferLocation = vertexGpuAddress;
            vertexBufferView.SizeInBytes = vertexData.Length;
            vertexBufferView.StrideInBytes = vertexStride;
            m_commandList.IASetVertexBuffers(0, vertexBufferView);
        }

        if (indexData != null)
        {
            readonlyBufferAllocator.Upload(indexData, out ulong indexGpuAddress);
            IndexBufferView indexBufferView = new IndexBufferView
            {
                BufferLocation = indexGpuAddress,
                SizeInBytes = indexData.Length,
                Format = (indexStride) == 2 ? Format.R16_UInt : Format.R32_UInt
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

    public void ReadBack(Texture2D texture2D, TextureDataCallback callback, object tag)
    {
        int RowPitch = (texture2D.width * 4 + 255) & ~255;
        int size = RowPitch * texture2D.height;

        graphicsDevice.fastBufferAllocatorReadBack.GetTemporaryBuffer(size, out var targetResource, out int offset, out var gpuAddress);

        var source = texture2D.resource;
        texture2D.SetAllResourceState(m_commandList, ResourceStates.CopySource);
        PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
        footPrint.Footprint.Width = texture2D.width;
        footPrint.Footprint.Height = texture2D.height;
        footPrint.Footprint.Depth = 1;
        footPrint.Footprint.RowPitch = RowPitch;
        footPrint.Footprint.Format = texture2D.format;
        footPrint.Offset = (ulong)offset;

        TextureCopyLocation Dst = new TextureCopyLocation(targetResource, footPrint);
        TextureCopyLocation Src = new TextureCopyLocation(source, 0);
        m_commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
        graphicsDevice.commandQueue.readBackCallbacks.Add(new ReadBackCallbackData()
        {
            callback = callback,
            resource = targetResource,
            frame = graphicsDevice.commandQueue.currentFenceValue,
            imageSize = size,
            offset = offset,
            rowPitch = RowPitch,
            width = texture2D.width,
            height = texture2D.height,
            format = texture2D.format,
            tag = tag
        });
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
    void PipelineBindingCompute2()
    {
        if (_currentComputeRootSignature != currentRootSignature)
        {
            _currentComputeRootSignature = currentRootSignature;
            _currentGraphicsRootSignature = null;
            m_commandList.SetComputeRootSignature(currentRootSignature.rootSignature);
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

    public void SetComputeResources(Action<ComputeResourceProxy> setResources)
    {
        var computeResourceProxy = new ComputeResourceProxy()
        {
            graphicsContext = this,
            cbvs = currentRootSignature.cbv,
            srvs = currentRootSignature.srv,
            uavs = currentRootSignature.uav,
        };
        setResources(computeResourceProxy);
    }

    public void Dispatch(int x, int y, int z)
    {
        m_commandList.Dispatch(x, y, z);
    }

    public void Execute()
    {
        foreach (var val in presents)
        {
            val.EndRenderTarget(m_commandList);
        }
        presents.Clear();

        var commandQueue = graphicsDevice.commandQueue;
        var copyCommandQueue = graphicsDevice.copyCommandQueue;
        graphicsDevice.superRingBuffer.DelayCommands(m_copyCommandList, commandQueue);
        graphicsDevice.fastBufferAllocator.FrameEnd();
        graphicsDevice.fastBufferAllocatorUAV.FrameEnd();
        graphicsDevice.fastBufferAllocatorReadBack.FrameEnd();
        m_copyCommandList.Close();
        copyCommandQueue.ExecuteCommandList(m_copyCommandList);
        copyCommandQueue.NextExecuteIndex();
        m_commandList.Close();
        commandQueue.WaitFor(copyCommandQueue);

        commandQueue.ExecuteCommandList(m_commandList);
        m_commandList = null;

        foreach (var resource in referenceThisCommand)
        {
            commandQueue.ResourceDelayRecycle(resource);
        }
        referenceThisCommand.Clear();
        // 提高帧索引。
        commandQueue.NextExecuteIndex();
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
        readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), out ulong addr);
        writer.Write(addr);
    }

    internal GpuDescriptorHandle GetUAVHandle(Texture2D texture, ResourceStates state = ResourceStates.UnorderedAccess)
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

    internal GpuDescriptorHandle GetUAVHandle(GPUBuffer buffer)
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
    internal GpuDescriptorHandle GetSRVHandle(GPUBuffer buffer)
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
    internal GpuDescriptorHandle GetSRVHandle(Texture2D texture, bool linear = false)
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

    internal GpuDescriptorHandle GetSRVHandleWithMip(Texture2D texture, int mips)
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

    internal GpuDescriptorHandle CreateSRV(ID3D12Resource resource, ShaderResourceViewDescription srvDesc)
    {
        graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
        graphicsDevice.device.CreateShaderResourceView(resource, srvDesc, cpuHandle);
        Reference(resource);
        return gpuHandle;
    }

    internal GpuDescriptorHandle CreateUAV(ID3D12Resource resource, UnorderedAccessViewDescription? uavDesc)
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

    internal FastBufferAllocator readonlyBufferAllocator => graphicsDevice.fastBufferAllocator;
}
