using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Runtime.InteropServices;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    internal sealed class RingBuffer : IDisposable
    {
        public unsafe void Init(ID3D12Device device, int size)
        {
            this.size = (size + 255) & ~255;

            ThrowIfFailed(device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None,
                ResourceDescription.Buffer((ulong)size), ResourceStates.GenericRead, out resource));
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

        IntPtr Upload(int size, out ulong gpuAddress)
        {
            int offset1 = GetOffsetAndMove(size);
            IntPtr result = mapped + offset1;
            gpuAddress = resource.GPUVirtualAddress + (ulong)offset1;
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

        public unsafe void Upload<T>(ReadOnlySpan<T> data, out ulong gpuAddress) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
            var range = new Span<T>(Upload(size1, out gpuAddress).ToPointer(), data.Length);
            data.CopyTo(range);
        }

        public unsafe void Upload<T>(IReadOnlyList<T> data, out ulong gpuAddress) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T)) * data.Count;
            var range = new Span<T>(Upload(size1, out gpuAddress).ToPointer(), data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                range[i] = data[i];
            }
        }

        public void Dispose()
        {
            resource?.Unmap(0);
            mapped = IntPtr.Zero;
            resource?.Release();
            resource = null;
        }

        IntPtr mapped;
        int size;
        int currentPosition;
        ID3D12Resource resource;
    }
}
