using System;
using System.Collections.Generic;

namespace Coocoo3DGraphics.Management;

public class DX12ResourceManager
{
    public DX12Resource DX12Resource;
    public GraphicsDevice GraphicsDevice;

    public void Initialize()
    {

    }

    //public SwapChain CreateSwapChain(IntPtr hwnd, int width, int height)
    //{
    //    SwapChain swapChain = new SwapChain();
    //    swapChain.Initialize(GraphicsDevice, hwnd, width, height);
    //    return swapChain;
    //}

    public void InitializeSwapChain(SwapChain swapChain, IntPtr hwnd, int width, int height)
    {
        var desc = DX12Resource.SwapChainDescriptions[0].GetSwapChainDescription1();
        desc.Width = width;
        desc.Height = height;
        swapChain.Initialize(GraphicsDevice, hwnd, desc);
    }
}
