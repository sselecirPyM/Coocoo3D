using System;
using System.Collections.Generic;
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
    ulong rPtr;
    ulong lPtr;
    ulong rMax;
    ulong[] positions = new ulong[3];

    ID3D12Resource resource;
    int frameIndex;

    List<CopyCommanding> copyCommands = new List<CopyCommanding>();

    internal CommandQueue copyCommandQueue;
    internal ID3D12GraphicsCommandList copyCommandList;


    public unsafe void Initialize(ID3D12Device device, int size, CommandQueue copyCommandQueue)
    {
        this.device = device;
        this.size = (size + 255) & ~255;
        this.copyCommandQueue = copyCommandQueue;
        this.copyCommandList = this.copyCommandQueue.GetCommandList();

        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)this.size), ResourceStates.GenericRead, out resource));


        void* ptr1 = null;
        resource.Map(0, &ptr1);
        mapped = new IntPtr(ptr1);
    }

    internal void CopyCommand(ID3D12Resource src, int srcOffset, int _size, ID3D12Resource dst, int dstOffset)
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

    public void DelayUploadTo(ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset = 0)
    {
        var data1 = data;
        int remain = data.Length;
        int used = 0;
        while (remain > 0)
        {
            CopyRegion(data1, dst, dstOffset, ref used, out bool refresh);
            remain = data.Length - used;
            if (remain > 0)
            {
                data1 = data.Slice(used);
            }
            if (refresh)
            {
                FrameEnd();
                FrameBegin();
            }
        }
    }
    unsafe void CopyRegion(ReadOnlySpan<byte> bytes, ID3D12Resource dst, int dstOffset, ref int used, out bool refresh)
    {
        ulong rightBound = (rPtr + (ulong)this.size) / (ulong)this.size * (ulong)this.size;

        int srcOffset = (int)CurrentPosition();
        int copySize = Math.Min(Math.Min(bytes.Length, (int)(rightBound - rPtr)), (int)(rMax - rPtr));
        int offsetSize = (copySize + 63) & ~63;
        bytes.Slice(0, copySize).CopyTo(new Span<byte>((void*)((ulong)mapped + (ulong)srcOffset), copySize));
        CopyCommand(resource, srcOffset, copySize, dst, dstOffset + used);

        used += copySize;
        rPtr += (ulong)offsetSize;
        refresh = rPtr >= rMax;
    }
    ulong CurrentPosition()
    {
        return rPtr % (ulong)this.size;
    }

    public void FrameBegin()
    {
        copyCommandList = copyCommandQueue.GetCommandList();
        copyCommandList.Reset(copyCommandQueue.GetCommandAllocator());
    }

    public void FrameEnd()
    {
        //see: https://learn.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-direct3d-12#implicit-state-transitions
        foreach (var command in copyCommands)
        {
            copyCommandList.CopyBufferRegion(command.dstResource, command.dstOffset, command.srcResource, command.srcOffset, command.numBytes);
        }

        positions[frameIndex] = rPtr;
        frameIndex = (frameIndex + 1) % 3;
        lPtr = positions[frameIndex];
        //rMax = Math.Min(lPtr + (ulong)this.size, rPtr + (ulong)this.size / 2);
        rMax = lPtr + (ulong)this.size;

        copyCommands.Clear();


        copyCommandList.Close();
        copyCommandQueue.ExecuteCommandList(copyCommandList);
        copyCommandQueue.NextExecuteIndex();
    }

    public void Dispose()
    {
        resource?.Unmap(0);
        mapped = IntPtr.Zero;
        resource?.Release();
        resource = null;
    }
}
