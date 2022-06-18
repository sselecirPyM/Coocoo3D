using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
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
        public List<ResourceStates> resourceStates = new List<ResourceStates>();
        public GraphicsObjectStatus Status;

        public void InitResourceState(ResourceStates rs)
        {
            resourceStates.Clear();
            for (int i = 0; i < mipLevels; i++)
                resourceStates.Add(rs);
        }

        internal void SetAllResourceState(ID3D12GraphicsCommandList commandList, ResourceStates states)
        {
            ResourceStates prev;

            prev = resourceStates[0];
            bool oneTrans = true;

            for (int i = 0; i < mipLevels; i++)
                if (resourceStates[i] != prev)
                    oneTrans = false;
            if (oneTrans)
            {
                if (states != prev)
                {
                    commandList.ResourceBarrierTransition(resource, prev, states);
                    for (int i = 0; i < mipLevels; i++)
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
                for (int i = 0; i < mipLevels; i++)
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
                int index1 = i;
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
        }

        public void ReloadAsRTVUAV(int width, int height, Format format) => ReloadAsRTVUAV(width, height, 1, format);
        public void ReloadAsRTVUAV(int width, int height, int mipLevels, Format format)
        {
            this.width = width;
            this.height = height;
            this.format = format;
            this.dsvFormat = Format.Unknown;
            this.rtvFormat = format;
            this.uavFormat = format;
            this.mipLevels = mipLevels;
        }

        public Format GetFormat()
        {
            if (dsvFormat != Format.Unknown)
                return dsvFormat;
            return format;
        }

        internal ResourceDescription GetResourceDescription()
        {
            ResourceDescription textureDesc = new ResourceDescription();
            textureDesc.MipLevels = (ushort)mipLevels;
            if (dsvFormat != Format.Unknown)
                textureDesc.Format = dsvFormat;
            else
                textureDesc.Format = format;
            textureDesc.Width = (ulong)width;
            textureDesc.Height = height;
            textureDesc.Flags = ResourceFlags.None;

            if (dsvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowDepthStencil;
            if (rtvFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowRenderTarget;
            if (uavFormat != Format.Unknown)
                textureDesc.Flags |= ResourceFlags.AllowUnorderedAccess;

            textureDesc.DepthOrArraySize = 1;
            textureDesc.SampleDescription.Count = 1;
            textureDesc.SampleDescription.Quality = 0;
            textureDesc.Dimension = ResourceDimension.Texture2D;
            return textureDesc;
        }

        public void Dispose()
        {
            resource?.Release();
            resource = null;
            Status = GraphicsObjectStatus.unload;
        }
    }
}
