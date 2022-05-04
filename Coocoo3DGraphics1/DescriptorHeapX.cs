using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class DescriptorHeapX : IDisposable
    {
        public GraphicsDevice graphicsDevice;
        public ID3D12DescriptorHeap heap;
        public int allocatedCount;
        public int descriptorCount;
        public int IncrementSize;

        public void Initialize(GraphicsDevice graphicsDevice, DescriptorHeapDescription descriptorHeapDescription)
        {
            this.graphicsDevice = graphicsDevice;
            allocatedCount = 0;
            descriptorCount = descriptorHeapDescription.DescriptorCount;
            ThrowIfFailed(graphicsDevice.device.CreateDescriptorHeap(descriptorHeapDescription, out heap));
            IncrementSize = graphicsDevice.device.GetDescriptorHandleIncrementSize(descriptorHeapDescription.Type);
        }


        public void GetTempHandle(out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle)
        {
            CpuDescriptorHandle cpuHandle1 = heap.GetCPUDescriptorHandleForHeapStart();
            cpuHandle1.Ptr += allocatedCount * IncrementSize;
            GpuDescriptorHandle gpuHandle1 = heap.GetGPUDescriptorHandleForHeapStart();
            gpuHandle1.Ptr += (ulong)(allocatedCount * IncrementSize);

            allocatedCount = (allocatedCount + 1) % descriptorCount;
            cpuHandle = cpuHandle1;
            gpuHandle = gpuHandle1;
        }


        public CpuDescriptorHandle GetTempCpuHandle()
        {
            CpuDescriptorHandle cpuHandle1 = heap.GetCPUDescriptorHandleForHeapStart();
            cpuHandle1.Ptr += allocatedCount * IncrementSize;

            allocatedCount = (allocatedCount + 1) % descriptorCount;
            return cpuHandle1;
        }

        public void Dispose()
        {
            heap?.Dispose();
            heap = null;
        }
    }
}