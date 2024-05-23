using System;
using System.Collections.Generic;
using System.Threading;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics;

internal class FrameAllocatorResource
{
    public ulong endFrame;
    public ID3D12CommandAllocator allocator;
    public FrameAllocatorResource(ulong endFrame, ID3D12CommandAllocator allocator)
    {
        this.endFrame = endFrame;
        this.allocator = allocator;
    }
}
internal sealed class CommandQueue : IDisposable
{
    internal ID3D12CommandQueue commandQueue;

    internal List<FrameAllocatorResource> commandAllocators = new List<FrameAllocatorResource>();

    internal uint executeIndex = 0;

    ID3D12GraphicsCommandList4 m_commandList;

    ID3D12Device device;

    CommandListType commandListType;

    internal ID3D12Fence fence;

    internal UInt64 currentFenceValue = 3;

    EventWaitHandle fenceEvent;

    internal List<(ID3D12Object, ulong)> m_recycleList = new List<(ID3D12Object, ulong)>();

    internal List<ReadBackCallbackData> readBackCallbacks = new List<ReadBackCallbackData>();

    public void Initialize(ID3D12Device device, CommandListType commandListType)
    {
        this.device = device;
        this.commandListType = commandListType;
        ThrowIfFailed(device.CreateCommandQueue(new CommandQueueDescription(commandListType), out commandQueue));
        for (int i = 0; i < c_frameCount; i++)
        {
            ThrowIfFailed(device.CreateCommandAllocator(commandListType, out ID3D12CommandAllocator commandAllocator));
            commandAllocators.Add(new FrameAllocatorResource(0, commandAllocator));
        }
        ThrowIfFailed(device.CreateFence(c_frameCount, FenceFlags.None, out fence));
        fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        currentFenceValue++;
    }

    public SharpGen.Runtime.Result Signal(ulong value) => commandQueue.Signal(fence, value);

    public SharpGen.Runtime.Result Wait(ID3D12Fence fence, ulong value) => commandQueue.Wait(fence, value);

    public void ExecuteCommandList(ID3D12CommandList commandList) => commandQueue.ExecuteCommandList(commandList);

    public ID3D12CommandAllocator GetCommandAllocator() => commandAllocators[(int)executeIndex].allocator;

    public void Wait()
    {
        // 在队列中安排信号命令。
        Signal(currentFenceValue);

        // 等待跨越围栏。
        fence.SetEventOnCompletion(currentFenceValue, fenceEvent);
        fenceEvent.WaitOne();

        // 对当前帧递增围栏值。
        currentFenceValue++;
    }

    /// <summary>
    ///  Gpu side wait.
    /// </summary>
    public void WaitFor(CommandQueue other)
    {
        commandQueue.Wait(other.fence, other.currentFenceValue - 1);
    }

    public void NextExecuteIndex()
    {
        Signal(currentFenceValue);
        executeIndex = (executeIndex < (c_frameCount - 1)) ? (executeIndex + 1) : 0;

        // 检查下一帧是否准备好启动。
        if (fence.CompletedValue < currentFenceValue - c_frameCount + 1)
        {
            fence.SetEventOnCompletion(currentFenceValue - c_frameCount + 1, fenceEvent);
            fenceEvent.WaitOne();
        }
        commandAllocators[(int)executeIndex].allocator.Reset();
        currentFenceValue++;
        Recycle();
    }


    internal ID3D12GraphicsCommandList4 GetCommandList()
    {
        if (m_commandList != null)
        {
            return m_commandList;
        }
        else
        {
            ThrowIfFailed(device.CreateCommandList(0, commandListType, GetCommandAllocator(), null, out m_commandList));
            m_commandList.Close();
            return m_commandList;
        }
    }

    internal void ResourceDelayRecycle(ID3D12Object resource)
    {
        if (resource != null)
            m_recycleList.Add((resource, currentFenceValue));
    }

    internal void Recycle()
    {
        var fence = this.fence;
        ulong completedFrame = fence.CompletedValue;
        readBackCallbacks.RemoveAll(x =>
        {
            if (x.frame <= completedFrame)
            {
                x.Call();
                return true;
            }
            return false;
        });
        m_recycleList.RemoveAll(x =>
        {
            if (x.Item2 <= completedFrame)
            {
                x.Item1.Release();
                return true;
            }
            return false;
        });
    }

    public void Dispose()
    {
        Recycle();
        commandQueue?.Release();
        commandQueue = null;
        if (commandAllocators != null)
            foreach (var allocator in commandAllocators)
                allocator.allocator.Release();
        m_commandList?.Release();
        m_commandList = null;
        fence?.Release();
        fence = null;
        fenceEvent.Dispose();
        fenceEvent = null;
    }
}
