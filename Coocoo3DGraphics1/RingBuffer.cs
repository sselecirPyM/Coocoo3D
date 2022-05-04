using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Runtime.InteropServices;

namespace Coocoo3DGraphics
{
    public class RingBuffer : IDisposable
    {
        public void Init(GraphicsDevice device, int size)
        {
            this.size = (size + 255) & ~255;

            device.device.CreateCommittedResource<ID3D12Resource>(new HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer((ulong)size), ResourceStates.GenericRead, out resource);
            mapped = resource.Map(0);
        }

        IntPtr Upload(ID3D12GraphicsCommandList commandList, int size, ID3D12Resource target, int offset)
        {
            if (currentPosition + size > this.size)
            {
                currentPosition = 0;
            }
            IntPtr result = mapped + currentPosition;
            commandList.CopyBufferRegion(target, (ulong)offset, resource, (ulong)currentPosition, (ulong)size);
            currentPosition = ((currentPosition + size + 255) & ~255) % this.size;

            return result;
        }

        public unsafe void Upload<T>(ID3D12GraphicsCommandList commandList, Span<T> data, ID3D12Resource target, int offset = 0) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T)) * data.Length;
            IntPtr ptr = Upload(commandList, size1, target, offset);
            var range = new Span<T>(ptr.ToPointer(), data.Length);
            data.CopyTo(range);
        }

        IntPtr Upload(int size, out ulong gpuAddress)
        {
            if (currentPosition + size > this.size)
            {
                currentPosition = 0;
            }
            IntPtr result = mapped + currentPosition;
            gpuAddress = resource.GPUVirtualAddress + (ulong)currentPosition;
            currentPosition = ((currentPosition + size + 255) & ~255) % this.size;

            return result;
        }

        public unsafe void Upload<T>(Span<T> data, out ulong gpuAddress) where T : unmanaged
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
            resource?.Release();
            resource = null;
        }

        IntPtr mapped;
        int size;
        int currentPosition;
        ID3D12Resource resource;
    }
}
