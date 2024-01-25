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

    public bool MergeCommand(CopyCommanding other)
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
    IntPtr mapped;
    int size;
    int currentPosition;
    int cbvSize;
    int cbvCurrentPosition;
    ID3D12Resource resource;
    ID3D12Resource[] cbufferResource;
    int frameIndex;

    public List<CopyCommanding> copyCommands = new List<CopyCommanding>();

    public unsafe void Initialize(ID3D12Device device, int size, int cbufferSize)
    {
        this.size = (size + 255) & ~255;
        this.cbvSize = (cbufferSize + 255) & ~255;

        ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)this.size), ResourceStates.GenericRead, out resource));

        cbufferResource = new ID3D12Resource[3];
        for (int i = 0; i < 3; i++)
            ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Default), HeapFlags.None,
                ResourceDescription.Buffer((ulong)this.cbvSize), ResourceStates.Common, out cbufferResource[i]));


        void* ptr1 = null;
        resource.Map(0, &ptr1);
        mapped = new IntPtr(ptr1);
    }

    IntPtr GetUploadBuffer(ID3D12GraphicsCommandList commandList, int size, ID3D12Resource target, int offset)
    {
        int offset1 = UploadGetOffsetAndMove(size);
        IntPtr result = mapped + offset1;
        commandList.CopyBufferRegion(target, (ulong)offset, resource, (ulong)offset1, (ulong)size);

        return result;
    }

    public unsafe void UploadTo<T>(ID3D12GraphicsCommandList commandList, ReadOnlySpan<T> data, ID3D12Resource target, int offset = 0) where T : unmanaged
    {
        int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
        IntPtr ptr = GetUploadBuffer(commandList, size1, target, offset);
        var range = new Span<T>(ptr.ToPointer(), data.Length);
        data.CopyTo(range);
    }

    IntPtr Upload(int size, out ulong gpuAddress, out int offset)
    {
        int offset1 = UploadGetOffsetAndMove(size);
        IntPtr result = mapped + offset1;
        gpuAddress = resource.GPUVirtualAddress + (ulong)offset1;
        offset = offset1;
        return result;
    }

    void DelayCopy(int srcOffset, int size, out ulong gpuAddress)
    {
        int offset1 = CBVGetOffsetAndMove(size);
        gpuAddress = cbufferResource[frameIndex].GPUVirtualAddress + (ulong)offset1;
        var copyCommanding = new CopyCommanding
        {
            dstOffset = (ulong)offset1,
            srcOffset = (ulong)srcOffset,
            numBytes = (ulong)size,
            srcResource = resource,
            dstResource = cbufferResource[frameIndex]
        };
        CopyCommanding delayCopy1;
        if (copyCommands.Count > 0 && copyCommanding.MergeCommand(delayCopy1 = copyCommands[^1]))
        {
            delayCopy1.numBytes += copyCommanding.numBytes;
            copyCommands[^1] = delayCopy1;
        }
        else
        {
            copyCommands.Add(copyCommanding);
        }
    }

    public unsafe void UploadBuffer<T>(ReadOnlySpan<T> data, out ulong gpuAddress) where T : unmanaged
    {
        int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
        size1 = (size1 + 255) & ~255;
        var range = new Span<T>(Upload(size1, out var gpuAddress1, out var srcOffset).ToPointer(), data.Length);
        data.CopyTo(range);
        DelayCopy(srcOffset, size1, out gpuAddress);
    }

    int UploadGetOffsetAndMove(int size)
    {
        if (currentPosition + size > this.size)
        {
            currentPosition = 0;
        }
        int result = currentPosition;
        currentPosition = ((currentPosition + size + 255) & ~255) % this.size;
        return result;
    }

    int CBVGetOffsetAndMove(int size)
    {
        if (cbvCurrentPosition + size > this.cbvSize)
        {
            cbvCurrentPosition = 0;
        }
        int result = cbvCurrentPosition;
        cbvCurrentPosition = ((cbvCurrentPosition + size + 255) & ~255) % this.cbvSize;
        return result;
    }

    public void DelayCommands(ID3D12GraphicsCommandList commandList)
    {
        //see: https://learn.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-direct3d-12#implicit-state-transitions
        foreach (var command in copyCommands)
        {
            commandList.CopyBufferRegion(command.dstResource, command.dstOffset, command.srcResource, command.srcOffset, command.numBytes);
        }
        frameIndex = (frameIndex + 1) % 3;
        cbvCurrentPosition = 0;
        copyCommands.Clear();
    }

    public void Dispose()
    {
        resource?.Unmap(0);
        mapped = IntPtr.Zero;
        resource?.Release();
        resource = null;
        if (cbufferResource != null)
            foreach (var cbuffer in cbufferResource)
                cbuffer?.Release();
        cbufferResource = null;
    }
}
