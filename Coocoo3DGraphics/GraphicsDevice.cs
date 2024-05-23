using SharpGen.Runtime;
using System;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

public sealed class GraphicsDevice : IDisposable
{
    public const int CBVSRVUAVDescriptorCount = 65536;
    internal ID3D12Device5 device;
    internal IDXGIAdapter adapter;

    internal DescriptorHeapX cbvsrvuavHeap;
    internal DescriptorHeapX rtvHeap;
    internal DescriptorHeapX dsvHeap;

    internal RingBuffer superRingBuffer = new RingBuffer();
    internal FastBufferAllocator fastBufferAllocator;
    internal FastBufferAllocator fastBufferAllocatorUAV;
    internal FastBufferAllocator fastBufferAllocatorReadBack;

    internal ID3D12Resource scratchResource;

    internal CommandQueue commandQueue;
    internal CommandQueue copyCommandQueue;

    string m_deviceDescription;
    UInt64 m_deviceVideoMem;

    internal IDXGIFactory6 m_dxgiFactory;

    bool m_isRayTracingSupport;

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


        commandQueue = new CommandQueue();
        commandQueue.Initialize(device, CommandListType.Direct);
        copyCommandQueue = new CommandQueue();
        copyCommandQueue.Initialize(device, CommandListType.Copy);

        DescriptorHeapDescription descriptorHeapDescription;
        descriptorHeapDescription.NodeMask = 0;

        descriptorHeapDescription.DescriptorCount = CBVSRVUAVDescriptorCount;
        descriptorHeapDescription.Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView;
        descriptorHeapDescription.Flags = DescriptorHeapFlags.ShaderVisible;
        cbvsrvuavHeap = new DescriptorHeapX();
        cbvsrvuavHeap.Initialize(device, descriptorHeapDescription);

        descriptorHeapDescription.DescriptorCount = 16;
        descriptorHeapDescription.Type = DescriptorHeapType.RenderTargetView;
        descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
        rtvHeap = new DescriptorHeapX();
        rtvHeap.Initialize(device, descriptorHeapDescription);

        descriptorHeapDescription.DescriptorCount = 16;
        descriptorHeapDescription.Type = DescriptorHeapType.DepthStencilView;
        descriptorHeapDescription.Flags = DescriptorHeapFlags.None;
        dsvHeap = new DescriptorHeapX();
        dsvHeap.Initialize(device, descriptorHeapDescription);

        superRingBuffer.Initialize(this.device, 134217728);
        fastBufferAllocator = new FastBufferAllocator(superRingBuffer, cbvsrvuavHeap, ResourceFlags.None);
        fastBufferAllocatorUAV = new FastBufferAllocator(superRingBuffer, cbvsrvuavHeap, ResourceFlags.AllowUnorderedAccess);
        fastBufferAllocatorReadBack = new FastBufferAllocator(superRingBuffer, null, HeapType.Readback, ResourceFlags.None);
    }

    public void WaitForGpu()
    {
        commandQueue.Wait();

        commandQueue.Recycle();
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
        return commandQueue.currentFenceValue;
    }

    public ulong GetInternalCompletedFenceValue()
    {
        var fence = commandQueue.fence;
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

    internal CpuDescriptorHandle GetRenderTargetView(ID3D12Resource resource, int mipMap)
    {
        var cpuHandle = rtvHeap.GetTempCpuHandle();
        RenderTargetViewDescription description = new RenderTargetViewDescription();
        description.ViewDimension = RenderTargetViewDimension.Texture2D;
        description.Texture2D.MipSlice = mipMap;
        device.CreateRenderTargetView(resource, description, cpuHandle);
        return cpuHandle;
    }

    internal CpuDescriptorHandle GetDepthStencilView(ID3D12Resource resource, int mipMap)
    {
        var cpuHandle = dsvHeap.GetTempCpuHandle();
        DepthStencilViewDescription description = new DepthStencilViewDescription();
        description.ViewDimension = DepthStencilViewDimension.Texture2D;
        description.Texture2D.MipSlice = mipMap;
        device.CreateDepthStencilView(resource, description, cpuHandle);
        return cpuHandle;
    }

    public void Dispose()
    {
        commandQueue?.Dispose();
        copyCommandQueue?.Dispose();
        fastBufferAllocatorUAV?.Dispose();
        fastBufferAllocator?.Dispose();
        fastBufferAllocatorReadBack?.Dispose();
        superRingBuffer?.Dispose();
        scratchResource?.Release();
        cbvsrvuavHeap?.Dispose();
        rtvHeap?.Dispose();
        dsvHeap?.Dispose();

#if DEBUG
        var debugDevice = device.QueryInterface<ID3D12DebugDevice1>();
        debugDevice.ReportLiveDeviceObjects(ReportLiveDeviceObjectFlags.Detail);
        debugDevice.Release();
#endif

        device?.Release();
        adapter?.Release();
    }
}
