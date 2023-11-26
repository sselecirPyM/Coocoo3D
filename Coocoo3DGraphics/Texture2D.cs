using System;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics;

public class Texture2D : IDisposable
{
    public ID3D12Resource resource;
    public string Name;
    public int width;
    public int height;
    public int mipLevels;
    public Format format;
    public Format rtvFormat;
    public Format dsvFormat;
    public Format uavFormat;
    private ResourceStates[] resourceStates;
    public int arraySize = 1;

    public bool isCube;

    public GraphicsObjectStatus Status;

    public void RefCopyTo(Texture2D target)
    {
        resource?.AddRef();
        target.resource?.Release();
        target.resource = resource;
        target.width = width;
        target.height = height;
        target.mipLevels = mipLevels;
        target.format = format;
        target.rtvFormat = rtvFormat;
        target.dsvFormat = dsvFormat;
        target.uavFormat = uavFormat;
        target.resourceStates = resourceStates;
        target.arraySize = arraySize;
        target.isCube = isCube;
    }

    public void InitResourceState(ResourceStates rs)
    {
        int arrayLenght = mipLevels * arraySize;
        resourceStates = new ResourceStates[arrayLenght];
        Array.Fill(resourceStates, rs);
    }

    internal void SetAllResourceState(ID3D12GraphicsCommandList commandList, ResourceStates states)
    {
        ResourceStates prev;

        prev = resourceStates[0];
        bool oneTrans = true;

        for (int i = 0; i < resourceStates.Length; i++)
            if (resourceStates[i] != prev)
                oneTrans = false;
        if (oneTrans)
        {
            if (states != prev)
            {
                commandList.ResourceBarrierTransition(resource, prev, states);
                for (int i = 0; i < resourceStates.Length; i++)
                {
                    resourceStates[i] = states;
                }
            }
            else if (states == ResourceStates.UnorderedAccess)
            {
                commandList.ResourceBarrierUnorderedAccessView(resource);
            }
        }
        else
        {
            for (int i = 0; i < resourceStates.Length; i++)
            {
                if (states != resourceStates[i])
                {
                    commandList.ResourceBarrierTransition(resource, resourceStates[i], states, i);
                }
                //else if (states == ResourceStates.UnorderedAccess)
                //{
                //    commandList.ResourceBarrierUnorderedAccessView(resource);
                //}
                resourceStates[i] = states;
            }
            if (states == ResourceStates.UnorderedAccess && prev == states)
            {
                commandList.ResourceBarrierUnorderedAccessView(resource);
            }
        }
    }

    internal void SetPartResourceState(ID3D12GraphicsCommandList commandList, ResourceStates states, int mipLevelBegin, int mipLevels)
    {
        bool uavBarrier = false;
        for (int i = mipLevelBegin; i < mipLevelBegin + mipLevels; i++)
        {
            for (int j = 0; j < arraySize; j++)
            {
                int index1 = j * this.mipLevels + i;
                if (states != resourceStates[index1])
                {
                    commandList.ResourceBarrierTransition(resource, resourceStates[index1], states, index1);
                    resourceStates[index1] = states;
                }
                else if (states == ResourceStates.UnorderedAccess)
                {
                    uavBarrier = true;
                }
            }
        }
        if (uavBarrier)
            commandList.ResourceBarrierUnorderedAccessView(resource);
    }

    public void ReloadAsDSV(int width, int height, int mips, Format format)
    {
        this.width = width;
        this.height = height;
        if (format == Format.D24_UNorm_S8_UInt)
            this.format = Format.R24_UNorm_X8_Typeless;
        else if (format == Format.D32_Float)
            this.format = Format.R32_Float;
        this.dsvFormat = format;
        this.rtvFormat = Format.Unknown;
        this.mipLevels = mips;
        this.arraySize = 1;
    }

    public void ReloadAsRTVUAV(int width, int height, int mipLevels, int arraySize, Format format)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        this.dsvFormat = Format.Unknown;
        this.rtvFormat = format;
        this.uavFormat = format;
        this.mipLevels = mipLevels;
        this.arraySize = arraySize;
        if (arraySize == 6)
        {
            isCube = true;
        }
    }

    public Format GetFormat()
    {
        if (dsvFormat != Format.Unknown)
            return dsvFormat;
        return format;
    }

    internal ResourceDescription GetResourceDescription()
    {
        ResourceDescription textureDesc = new ResourceDescription
        {
            MipLevels = (ushort)mipLevels,
            Width = (ulong)width,
            Height = height,
            Dimension = ResourceDimension.Texture2D,
            DepthOrArraySize = (ushort)arraySize,
            Flags = ResourceFlags.None,
            SampleDescription = new SampleDescription(1, 0)
        };

        if (dsvFormat != Format.Unknown)
            textureDesc.Format = dsvFormat;
        else
            textureDesc.Format = format;

        if (dsvFormat != Format.Unknown)
            textureDesc.Flags |= ResourceFlags.AllowDepthStencil;
        if (rtvFormat != Format.Unknown)
            textureDesc.Flags |= ResourceFlags.AllowRenderTarget;
        if (uavFormat != Format.Unknown)
            textureDesc.Flags |= ResourceFlags.AllowUnorderedAccess;

        return textureDesc;
    }

    public void Dispose()
    {
        resource?.Release();
        resource = null;
        Status = GraphicsObjectStatus.unload;
    }
}
