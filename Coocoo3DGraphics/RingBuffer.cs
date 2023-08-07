using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Runtime.InteropServices;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

struct DelayCopyCommand
{
    public ulong dstOffset;
    public ulong srcOffset;
    public ulong numBytes;
    public ID3D12Resource resource;

    public bool CombineTest(DelayCopyCommand other)
    {
        if (dstOffset == other.dstOffset + other.numBytes &&
            srcOffset == other.srcOffset + other.numBytes &&
            resource == other.resource)
            return true;
        return false;
    }
}
internal sealed class RingBuffer : IDisposable
{
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
        int offset1 = GetOffsetAndMove(size);
        IntPtr result = mapped + offset1;
        commandList.CopyBufferRegion(target, (ulong)offset, resource, (ulong)offset1, (ulong)size);

        return result;
    }

    public unsafe void Upload<T>(ID3D12GraphicsCommandList commandList, ReadOnlySpan<T> data, ID3D12Resource target, int offset = 0) where T : unmanaged
    {
        int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
        IntPtr ptr = GetUploadBuffer(commandList, size1, target, offset);
        var range = new Span<T>(ptr.ToPointer(), data.Length);
        data.CopyTo(range);
    }

    IntPtr Upload(int size, out ulong gpuAddress, out int offset)
    {
        int offset1 = GetOffsetAndMove(size);
        IntPtr result = mapped + offset1;
        gpuAddress = resource.GPUVirtualAddress + (ulong)offset1;
        offset = offset1;
        return result;
    }

    int GetOffsetAndMove(int size)
    {
        if (currentPosition + size > this.size)
        {
            currentPosition = 0;
        }
        int result = currentPosition;
        currentPosition = ((currentPosition + size + 255) & ~255) % this.size;
        return result;
    }

    void CBufferCopy(int srcOffset, int size, out ulong gpuAddress)
    {
        int offset1 = CBVGetOffsetAndMove(size);
        gpuAddress = cbufferResource[cbufferIndex].GPUVirtualAddress + (ulong)offset1;
        var delayCopy = new DelayCopyCommand
        {
            dstOffset = (ulong)offset1,
            srcOffset = (ulong)srcOffset,
            numBytes = (ulong)size,
            resource = cbufferResource[cbufferIndex]
        };
        DelayCopyCommand delayCopy1;
        if (delayCopyCommands.Count > 0 &&
            delayCopy.CombineTest(delayCopy1 = delayCopyCommands[^1]))
        {
            delayCopy1.numBytes += delayCopy.numBytes;
            delayCopyCommands[^1] = delayCopy1;
        }
        else
            delayCopyCommands.Add(delayCopy);
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

    public unsafe void DelayUpload<T>(ReadOnlySpan<T> data, out ulong gpuAddress) where T : unmanaged
    {
        int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
        size1 = (size1 + 255) & ~255;
        var range = new Span<T>(Upload(size1, out var gpuAddress1, out var srcOffset).ToPointer(), data.Length);
        data.CopyTo(range);
        CBufferCopy(srcOffset, size1, out gpuAddress);
    }

    public List<DelayCopyCommand> delayCopyCommands = new List<DelayCopyCommand>();

    public void DelayCommands(ID3D12GraphicsCommandList commandList)
    {
        //see: https://learn.microsoft.com/en-us/windows/win32/direct3d12/using-resource-barriers-to-synchronize-resource-states-in-direct3d-12#implicit-state-transitions
        foreach (var command in delayCopyCommands)
        {
            commandList.CopyBufferRegion(command.resource, command.dstOffset, resource, command.srcOffset, command.numBytes);
        }
        cbufferIndex = (cbufferIndex + 1) % 3;
        cbvCurrentPosition = 0;
        delayCopyCommands.Clear();
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

    IntPtr mapped;
    int size;
    int currentPosition;
    int cbvSize;
    int cbvCurrentPosition;
    ID3D12Resource resource;
    ID3D12Resource[] cbufferResource;
    int cbufferIndex;
}
