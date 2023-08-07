using System;
using System.Collections.Generic;

namespace Coocoo3DGraphics.Management;

public class DX12ResourceManager : IDisposable
{
    public DX12Resource DX12Resource;
    public GraphicsDevice GraphicsDevice;

    public Dictionary<string, RootSignature> RootSignatures = new();

    public void Dispose()
    {
        foreach (var rootSignature in RootSignatures.Values)
        {
            rootSignature.Dispose();
        }
    }

    public RootSignature GetRootSignature(string s)
    {
        if (RootSignatures.TryGetValue(s, out RootSignature rs))
            return rs;
        rs = new RootSignature();
        rs.Load(RSFromString(s));
        RootSignatures[s] = rs;
        return rs;
    }
    static ResourceAccessType[] RSFromString(string s)
    {
        ResourceAccessType[] desc = new ResourceAccessType[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            desc[i] = c switch
            {
                'C' => ResourceAccessType.CBV,
                'c' => ResourceAccessType.CBVTable,
                'S' => ResourceAccessType.SRV,
                's' => ResourceAccessType.SRVTable,
                'U' => ResourceAccessType.UAV,
                'u' => ResourceAccessType.UAVTable,
                _ => throw new NotImplementedException("error root signature desc."),
            };
        }
        return desc;
    }

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
