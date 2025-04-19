using System;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

class BufferTracking
{
    public ID3D12Resource resource;
    public int size;
    public int offset;
    public ResourceStates resourceStates;

    public bool Allocate(int _size, int align, out int _offset, out ulong address)
    {
        offset = (offset + align - 1) & ~(align - 1);
        address = resource.GPUVirtualAddress + (uint)offset;
        _offset = offset;
        offset += _size;
        if (offset > size)
        {
            return false;
        }

        return true;
    }

    public void ToState(ID3D12GraphicsCommandList commandList, ResourceStates dstState)
    {
        if (dstState != resourceStates)
        {
            commandList.ResourceBarrier(ResourceBarrier.BarrierTransition(resource, resourceStates, dstState));
            resourceStates = dstState;
        }
        else if (dstState == ResourceStates.UnorderedAccess)
        {
            commandList.ResourceBarrierUnorderedAccessView(resource);
        }
    }

    public void Dispose()
    {
        resource?.Dispose();
        resource = null;
    }
}
class FastBufferAllocator : IDisposable
{
    RingBuffer ringBuffer;
    ID3D12Device device;
    DescriptorHeapX descriptorHeap;

    BufferTracking[] buffers;
    int frameIndex;
    ResourceFlags resourceFlags;
    HeapType heapType;
    ResourceStates resourceStates;
    CommandQueue commandQueue;

    internal FastBufferAllocator(RingBuffer ringBuffer, DescriptorHeapX cbvsrvuav, ResourceFlags resourceFlags, CommandQueue commandQueue)
    {
        this.ringBuffer = ringBuffer;
        this.device = ringBuffer.device;
        this.descriptorHeap = cbvsrvuav;
        this.resourceFlags = resourceFlags;
        heapType = HeapType.Default;
        this.commandQueue = commandQueue;

        buffers = new BufferTracking[3];
        for (int i = 0; i < 3; i++)
        {
            buffers[i] = new BufferTracking();
            buffers[i].size = 65536;
            CreateBuffer(buffers[i]);
        }
    }

    internal FastBufferAllocator(RingBuffer ringBuffer, DescriptorHeapX cbvsrvuav, HeapType heapType, ResourceFlags resourceFlags, CommandQueue commandQueue)
    {
        this.ringBuffer = ringBuffer;
        this.device = ringBuffer.device;
        this.descriptorHeap = cbvsrvuav;
        this.resourceFlags = resourceFlags;
        this.heapType = heapType;
        this.commandQueue = commandQueue;

        if (heapType == HeapType.Readback)
        {
            resourceStates = ResourceStates.CopyDest;
        }

        buffers = new BufferTracking[3];
        for (int i = 0; i < 3; i++)
        {
            buffers[i] = new BufferTracking();
            buffers[i].size = 65536;
            CreateBuffer(buffers[i]);
        }
    }

    public GpuDescriptorHandle GetSRV(ReadOnlySpan<byte> data)
    {
        var buffer = buffers[frameIndex];
        Allocate(buffer, data.Length, 16, out var dstOffset, out var gpuAddress);

        ringBuffer.DelayUploadTo(data, buffer.resource, dstOffset);

        ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription()
        {
            Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
            Format = Format.R32_Typeless,
            ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer,
            Buffer = new BufferShaderResourceView()
            {
                FirstElement = (ulong)dstOffset / 4,
                NumElements = data.Length / 4,
                Flags = BufferShaderResourceViewFlags.Raw,
            }
        };

        return CreateSRV(buffer.resource, srvDesc);
    }

    public GpuDescriptorHandle GetUAV(ReadOnlySpan<byte> data, out ulong gpuAddress)
    {
        var buffer = buffers[frameIndex];
        Allocate(buffer, data.Length, 16, out var dstOffset, out gpuAddress);

        ringBuffer.DelayUploadTo(data, buffer.resource, dstOffset);

        UnorderedAccessViewDescription uavDesc = new UnorderedAccessViewDescription()
        {
            Format = Format.R32_Typeless,
            ViewDimension = Vortice.Direct3D12.UnorderedAccessViewDimension.Buffer,
            Buffer = new BufferUnorderedAccessView()
            {
                FirstElement = (ulong)dstOffset / 4,
                NumElements = data.Length / 4,
                Flags = BufferUnorderedAccessViewFlags.Raw,
            }
        };

        return CreateUAV(buffer.resource, uavDesc);
    }

    internal void Upload(ReadOnlySpan<byte> data, out ulong gpuAddress, out BufferTracking buffer, out int _offset)
    {
        buffer = buffers[frameIndex];
        Allocate(buffer, data.Length, 4, out var dstOffset, out gpuAddress);

        ringBuffer.DelayUploadTo(data, buffer.resource, dstOffset);
        _offset = dstOffset;
    }

    internal void Upload(ReadOnlySpan<byte> data, out ulong gpuAddress)
    {
        var buffer = buffers[frameIndex];
        Allocate(buffer, data.Length, 16, out var dstOffset, out gpuAddress);

        ringBuffer.DelayUploadTo(data, buffer.resource, dstOffset);
    }

    internal void Upload(ReadOnlySpan<byte> data, int align, out ulong gpuAddress)
    {
        var buffer = buffers[frameIndex];
        Allocate(buffer, data.Length, align, out var dstOffset, out gpuAddress);

        ringBuffer.DelayUploadTo(data, buffer.resource, dstOffset);
    }

    internal void GetCopy(ID3D12GraphicsCommandList commandList, ID3D12Resource src, int srcOffset, int size, out ulong gpuAddress, out BufferTracking buffer, out int _offset)
    {
        buffer = buffers[frameIndex];
        Allocate(buffer, size, 16, out var dstOffset, out gpuAddress);
        buffer.ToState(commandList, ResourceStates.CopyDest);
        commandList.CopyBufferRegion(buffer.resource, (ulong)dstOffset, src, (ulong)srcOffset, (ulong)size);
        _offset = dstOffset;
    }

    internal void GetTemporaryBuffer(int size, out ID3D12Resource resource, out int _offset, out ulong gpuAddress)
    {
        var buffer = buffers[frameIndex];
        Allocate(buffer, size, 4, out _offset, out gpuAddress);
        resource = buffer.resource;
    }

    internal BufferTracking GetBufferTracking()
    {
        return buffers[frameIndex];
    }

    void Allocate(BufferTracking tracking, int _size, int align, out int _offset, out ulong address)
    {
        if (tracking.Allocate(_size, align, out _offset, out address))
        {
            return;
        }

        tracking.size *= 2;
        while (tracking.size < tracking.offset + _size)
            tracking.size *= 2;
        CreateBuffer(tracking);
        tracking.Allocate(_size, align, out _offset, out address);
    }

    void CreateBuffer(BufferTracking tracking)
    {
        if (tracking.resource != null)
        {
            commandQueue.ResourceDelayRecycle(tracking.resource);
        }
        tracking.offset = 0;
        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(heapType), HeapFlags.None,
            ResourceDescription.Buffer((ulong)tracking.size, resourceFlags), resourceStates, out tracking.resource));
    }

    GpuDescriptorHandle CreateSRV(ID3D12Resource resource, ShaderResourceViewDescription srvDesc)
    {
        descriptorHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
        device.CreateShaderResourceView(resource, srvDesc, cpuHandle);
        return gpuHandle;
    }

    GpuDescriptorHandle CreateUAV(ID3D12Resource resource, UnorderedAccessViewDescription uavDesc)
    {
        descriptorHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
        device.CreateUnorderedAccessView(resource, null, uavDesc, cpuHandle);
        return gpuHandle;
    }

    public void FrameEnd()
    {
        frameIndex = (frameIndex + 1) % 3;
        buffers[frameIndex].offset = 0;
    }

    public void Dispose()
    {
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
    }
}
