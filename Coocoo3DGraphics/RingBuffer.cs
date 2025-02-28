using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

struct CopyCommanding
{
    public ulong dstOffset;
    public ulong srcOffset;
    public ulong numBytes;
    public ID3D12Resource srcResource;
    public ID3D12Resource dstResource;

    public bool CanMerge(CopyCommanding other)
    {
        if (dstOffset == other.dstOffset + other.numBytes &&
            srcOffset == other.srcOffset + other.numBytes &&
            srcResource == other.srcResource &&
            dstResource == other.dstResource)
            return true;
        return false;
    }
}
internal sealed class RingBuffer : IDisposable
{
    public ID3D12Device device;
    IntPtr mapped;
    int size;
    int currentPosition;

    ID3D12Resource resource;
    int frameIndex;

    List<CopyCommanding> copyCommands = new List<CopyCommanding>();

    internal List<ID3D12Resource> needRecycle = new List<ID3D12Resource>();

    public unsafe void Initialize(ID3D12Device device, int size)
    {
        this.device = device;
        this.size = (size + 255) & ~255;

        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)this.size), ResourceStates.GenericRead, out resource));


        void* ptr1 = null;
        resource.Map(0, &ptr1);
        mapped = new IntPtr(ptr1);
    }

    public void UploadTo(ID3D12GraphicsCommandList commandList, ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset = 0)
    {
        var data1 = MemoryMarshal.AsBytes(data);
        var range = GetUploadRegion(data1.Length, out var srcOffset);
        data1.CopyTo(range);
        commandList.CopyBufferRegion(dst, (ulong)dstOffset, resource, (ulong)srcOffset, (ulong)data1.Length);
    }

    public void DelayUploadTo(ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset)
    {
        int _size = data.Length;
        var range = GetUploadRegion(_size, out var srcOffset);
        data.CopyTo(range);

        CopyCommand(resource, srcOffset, _size, dst, dstOffset);
    }

    public void CopyCommand(ID3D12Resource src, int srcOffset, int _size, ID3D12Resource dst, int dstOffset)
    {
        var copyCommanding = new CopyCommanding
        {
            dstOffset = (ulong)dstOffset,
            srcOffset = (ulong)srcOffset,
            numBytes = (ulong)_size,
            srcResource = src,
            dstResource = dst
        };
        CopyCommanding delayCopy1;
        if (copyCommands.Count > 0 && copyCommanding.CanMerge(delayCopy1 = copyCommands[^1]))
        {
            delayCopy1.numBytes += copyCommanding.numBytes;
            copyCommands[^1] = delayCopy1;
        }
        else
        {
            copyCommands.Add(copyCommanding);
        }
    }

    unsafe Span<byte> GetUploadRegion(int size, out int offset)
    {
        size = (size + 63) & ~63;
        offset = RingBufferAllocate(size);
        IntPtr result = mapped + offset;
        return new Span<byte>(result.ToPointer(), size);
    }

    int RingBufferAllocate(int size)
    {
        if (currentPosition + size > this.size)
        {
            currentPosition = 0;
        }
        int result = currentPosition;
        currentPosition = ((currentPosition + size + 255) & ~255) % this.size;
        return result;
    }

    public void DelayCommands(ID3D12GraphicsCommandList commandList, CommandQueue queue)
    {
        //see: https://learn.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-direct3d-12#implicit-state-transitions
        foreach (var command in copyCommands)
        {
            commandList.CopyBufferRegion(command.dstResource, command.dstOffset, command.srcResource, command.srcOffset, command.numBytes);
        }
        frameIndex = (frameIndex + 1) % 3;
        copyCommands.Clear();

        foreach (var resource in needRecycle)
        {
            queue.ResourceDelayRecycle(resource);
        }
        needRecycle.Clear();
    }

    public void Dispose()
    {
        resource?.Unmap(0);
        mapped = IntPtr.Zero;
        resource?.Release();
        resource = null;
    }
}
