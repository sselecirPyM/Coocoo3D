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
    int cbvSize;

    ID3D12Resource resource;
    BufferTracking[] cbuffers;
    int frameIndex;

    List<CopyCommanding> copyCommands = new List<CopyCommanding>();

    internal List<ID3D12Resource> needRecycle = new List<ID3D12Resource>();

    public unsafe void Initialize(ID3D12Device device, int size, int cbufferSize)
    {
        this.device = device;
        this.size = (size + 255) & ~255;
        this.cbvSize = (cbufferSize + 255) & ~255;

        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)this.size), ResourceStates.GenericRead, out resource));

        cbuffers = new BufferTracking[3];
        for (int i = 0; i < 3; i++)
        {
            cbuffers[i] = new BufferTracking();
            cbuffers[i].size = this.cbvSize;
            CreateBuffer(cbuffers[i]);
        }

        void* ptr1 = null;
        resource.Map(0, &ptr1);
        mapped = new IntPtr(ptr1);
    }

    void CreateBuffer(BufferTracking tracking)
    {
        if (tracking.resource != null)
        {
            needRecycle.Add(tracking.resource);
        }
        tracking.offset = 0;
        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Default), HeapFlags.None,
            ResourceDescription.Buffer((ulong)tracking.size), ResourceStates.Common, out tracking.resource));
    }

    void Allocate(BufferTracking tracking, int _size, out int _offset, out ulong address)
    {
        if (tracking.Allocate(_size, out _offset, out address))
        {
            return;
        }

        tracking.size *= 2;
        while (tracking.size < tracking.offset + _size)
            tracking.size *= 2;
        CreateBuffer(tracking);
        tracking.Allocate(_size, out _offset, out address);
    }

    public void UploadTo(ID3D12GraphicsCommandList commandList, ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset = 0)
    {
        var data1 = MemoryMarshal.AsBytes(data);
        var range = GetUploadRegion(data1.Length, out var srcOffset);
        data1.CopyTo(range);
        commandList.CopyBufferRegion(dst, (ulong)dstOffset, resource, (ulong)srcOffset, (ulong)data1.Length);
    }

    public void UploadBuffer<T>(ReadOnlySpan<T> data, out ulong gpuAddress) where T : unmanaged
    {
        var data1 = MemoryMarshal.AsBytes(data);

        int _size = (data1.Length + 255) & ~255;
        var tracking = cbuffers[frameIndex];
        Allocate(tracking, _size, out var dstOffset, out gpuAddress);
        DelayUploadTo(data1, tracking.resource, dstOffset);
    }

    public void DelayUploadTo(ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset)
    {
        int _size = data.Length;
        var range = GetUploadRegion(_size, out var srcOffset);
        data.CopyTo(range);

        DelayCopyTo(resource, srcOffset, _size, dst, dstOffset);
    }

    public void DelayUploadTo(ReadOnlySpan<byte> data, ID3D12Resource dst, int dstOffset, int align)
    {
        int _size = (data.Length + align - 1) & ~(align - 1);
        var range = GetUploadRegion(_size, out var srcOffset);
        data.CopyTo(range);

        DelayCopyTo(resource, srcOffset, _size, dst, dstOffset);
    }

    public void DelayCopyTo(ID3D12Resource src, int srcOffset, int _size, ID3D12Resource dst, int dstOffset)
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
        //size = (size + 255) & ~255;
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
        cbuffers[frameIndex].offset = 0;
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
        if (cbuffers != null)
            foreach (var cbuffer in cbuffers)
                cbuffer?.Dispose();
        cbuffers = null;
    }
}
