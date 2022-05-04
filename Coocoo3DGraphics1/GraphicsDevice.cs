using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Numerics;
using SharpGen.Runtime;
using Vortice.DXGI;
using System.Threading;
using Vortice.Direct3D12.Debug;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class GraphicsDevice
    {
        public struct recycleResource
        {
            public ID3D12Object resource;
            public ulong removeFrame;

            public recycleResource(ID3D12Object resource, ulong removeFrame)
            {
                this.resource = resource;
                this.removeFrame = removeFrame;
            }
        };

        public const int CBVSRVUAVDescriptorCount = 65536;
        internal ID3D12Device5 device;
        internal IDXGIAdapter adapter;

        internal DescriptorHeapX cbvsrvuavHeap;
        internal DescriptorHeapX rtvHeap;
        internal DescriptorHeapX dsvHeap;

        internal RingBuffer superRingBuffer = new RingBuffer();

        internal ID3D12Resource scratchResource;

        string m_deviceDescription;
        UInt64 m_deviceVideoMem;

        internal UInt64 currentFenceValue = 3;

        internal List<recycleResource> m_recycleList = new List<recycleResource>();
        List<ID3D12GraphicsCommandList4> m_commandLists = new List<ID3D12GraphicsCommandList4>();
        List<ID3D12GraphicsCommandList4> m_commandLists1 = new List<ID3D12GraphicsCommandList4>();

        internal IDXGIFactory6 m_dxgiFactory;

        internal ID3D12CommandQueue commandQueue;
        ID3D12CommandAllocator[] commandAllocators = new ID3D12CommandAllocator[c_frameCount];

        bool m_isRayTracingSupport;

        ID3D12Fence fence;
        EventWaitHandle fenceEvent;
        internal uint executeIndex = 0;

        public GraphicsDevice()
        {
            CreateDeviceResource();
        }

        internal void CreateDeviceResource()
        {
#if DEBUG
            if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
                pDx12Debug.EnableDebugLayer();
#endif
            ThrowIfFailed(DXGI.CreateDXGIFactory1(out m_dxgiFactory));

            int index1 = 0;
            while (true)
            {
                adapter?.Dispose();
                var hr = m_dxgiFactory.EnumAdapterByGpuPreference(index1, GpuPreference.HighPerformance, out adapter);
                if (hr == Result.Ok)
                {
                    var hr1 = D3D12.D3D12CreateDevice<ID3D12Device5>(this.adapter, out var device1);
                    if (hr1 == Result.Ok)
                    {
                        device?.Dispose();
                        device = device1;
                        break;
                    }
                }
                else if (hr == -2005270526)
                    throw new Exception("No direct3d12 device.");
                index1++;
            }
            m_deviceDescription = adapter.Description.Description;
            m_deviceVideoMem = (ulong)(long)adapter.Description.DedicatedVideoMemory;
            m_isRayTracingSupport = CheckRayTracingSupport(device);

            ThrowIfFailed(device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct), out commandQueue));

            DescriptorHeapDescription descriptorHeapDescription;
            descriptorHeapDescription.NodeMask = 0;

            descriptorHeapDescription.DescriptorCount = CBVSRVUAVDescriptorCount;
            descriptorHeapDescription.Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.ShaderVisible;
            cbvsrvuavHeap = new DescriptorHeapX();
            cbvsrvuavHeap.Initialize(this, descriptorHeapDescription);

            descriptorHeapDescription.DescriptorCount = 16;
            descriptorHeapDescription.Type = DescriptorHeapType.RenderTargetView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
            rtvHeap = new DescriptorHeapX();
            rtvHeap.Initialize(this, descriptorHeapDescription);

            descriptorHeapDescription.DescriptorCount = 16;
            descriptorHeapDescription.Type = DescriptorHeapType.DepthStencilView;
            descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
            dsvHeap = new DescriptorHeapX();
            dsvHeap.Initialize(this, descriptorHeapDescription);

            fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);


            for (int i = 0; i < c_frameCount; i++)
            {
                ThrowIfFailed(device.CreateCommandAllocator(CommandListType.Direct, out ID3D12CommandAllocator commandAllocator));
                commandAllocators[i] = commandAllocator;
            }
            ThrowIfFailed(device.CreateFence(currentFenceValue, FenceFlags.None, out fence));
            superRingBuffer.Init(this, 134217728);
            currentFenceValue++;
        }

        internal ID3D12GraphicsCommandList4 GetCommandList()
        {
            if (m_commandLists.Count > 0)
            {
                var commandList = m_commandLists[m_commandLists.Count - 1];
                m_commandLists.RemoveAt(m_commandLists.Count - 1);
                return commandList;
            }
            else
            {
                ID3D12GraphicsCommandList4 commandList;
                ThrowIfFailed(device.CreateCommandList(0, CommandListType.Direct, GetCommandAllocator(), null, out commandList));
                commandList.Close();
                return commandList;
            }
        }

        internal void ReturnCommandList(ID3D12GraphicsCommandList4 commandList)
        {
            m_commandLists1.Add(commandList);
        }

        internal void ResourceDelayRecycle(ID3D12Object resource)
        {
            if (resource != null)
                m_recycleList.Add(new recycleResource(resource, currentFenceValue));
        }

        public void RenderBegin()
        {
            GetCommandAllocator().Reset();
        }

        public void RenderComplete()
        {
            commandQueue.Signal(fence, currentFenceValue);

            // 提高帧索引。
            executeIndex = (executeIndex < (c_frameCount - 1)) ? (executeIndex + 1) : 0;

            // 检查下一帧是否准备好启动。
            if (fence.CompletedValue < currentFenceValue - c_frameCount + 1)
            {
                fence.SetEventOnCompletion(currentFenceValue - c_frameCount + 1, fenceEvent);
                fenceEvent.WaitOne();
            }
            Recycle();

            // 为下一帧设置围栏值。
            currentFenceValue++;
        }

        public void WaitForGpu()
        {
            // 在队列中安排信号命令。
            commandQueue.Signal(fence, currentFenceValue);

            // 等待跨越围栏。
            fence.SetEventOnCompletion(currentFenceValue, fenceEvent);
            fenceEvent.WaitOne();

            Recycle();

            // 对当前帧递增围栏值。
            currentFenceValue++;
        }

        void Recycle()
        {
            ulong completedFrame = fence.CompletedValue;
            m_recycleList.RemoveAll(x =>
            {
                if (x.removeFrame <= completedFrame)
                {
                    x.resource.Release();
                    return true;
                }
                return false;
            });

            m_commandLists.AddRange(m_commandLists1);
            m_commandLists1.Clear();
        }

        public bool IsRayTracingSupport()
        {
            return m_isRayTracingSupport;
        }

        public string GetDeviceDescription()
        {
            return m_deviceDescription;
        }

        public ulong GetDeviceVideoMemory()
        {
            return m_deviceVideoMem;
        }

        public ulong GetInternalFenceValue()
        {
            return currentFenceValue;
        }

        public ulong GetInternalCompletedFenceValue()
        {
            return fence.CompletedValue;
        }

        static bool CheckRayTracingSupport(ID3D12Device device)
        {
            FeatureDataD3D12Options5 featureDataD3D12Options5 = new FeatureDataD3D12Options5();
            var checkResult = device.CheckFeatureSupport(Vortice.Direct3D12.Feature.Options5, ref featureDataD3D12Options5);
            if (featureDataD3D12Options5.RaytracingTier == RaytracingTier.NotSupported)
                return false;
            else
                return true;
        }

        internal CpuDescriptorHandle GetRenderTargetView(ID3D12Resource resource)
        {
            var cpuHandle = rtvHeap.GetTempCpuHandle();
            device.CreateRenderTargetView(resource, null, cpuHandle);
            return cpuHandle;
        }

        internal CpuDescriptorHandle GetDepthStencilView(ID3D12Resource resource)
        {
            var cpuHandle = dsvHeap.GetTempCpuHandle();
            device.CreateDepthStencilView(resource, null, cpuHandle);
            return cpuHandle;
        }

        internal ID3D12CommandAllocator GetCommandAllocator() { return commandAllocators[executeIndex]; }
    }
}
