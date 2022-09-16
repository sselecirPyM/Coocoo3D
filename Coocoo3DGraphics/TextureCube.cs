using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class TextureCube : IDisposable
    {
        public ID3D12Resource resource;
        public string Name;
        public ID3D12DescriptorHeap renderTargetView;
        public ID3D12DescriptorHeap depthStencilView;
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
            for (int i = 0; i < mipLevels * 6; i++)
                resourceStates.Add(rs);
        }

        internal void SetAllResourceState(ID3D12GraphicsCommandList commandList, ResourceStates states)
        {
            ResourceStates prev;

            prev = resourceStates[0];
            bool oneTrans = true;

            for (int i = 0; i < mipLevels * 6; i++)
                if (resourceStates[i] != prev)
                    oneTrans = false;
            if (oneTrans)
            {
                if (states != prev)
                {
                    commandList.ResourceBarrierTransition(resource, prev, states);
                    for (int i = 0; i < resourceStates.Count; i++)
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
                for (int i = 0; i < resourceStates.Count; i++)
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
            for (int i = mipLevelBegin; i < mipLevelBegin + mipLevels; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    int index1 = j * this.mipLevels + i;
                    if (states != resourceStates[index1])
                    {
                        commandList.ResourceBarrierTransition(resource, resourceStates[index1], states, index1);
                        resourceStates[index1] = states;
                    }
                }
            }
        }

        internal void SetResourceState(ID3D12GraphicsCommandList commandList, ResourceStates states, int mipLevel, int faceIndex)
        {
            int index1 = faceIndex * this.mipLevels + mipLevel;
            if (states != resourceStates[index1])
            {
                commandList.ResourceBarrierTransition(resource, resourceStates[index1], states, index1);
                resourceStates[index1] = states;
            }
        }

        public void ReloadAsRTVUAV(int width, int height, Format format) => ReloadAsRTVUAV(width, height, 1, format);
        public void ReloadAsRTVUAV(int width, int height, int mipLevels, Format format)
        {
            this.width = width;
            this.height = height;
            this.mipLevels = mipLevels;
            this.format = format;
            this.dsvFormat = Format.Unknown;
            this.rtvFormat = format;
            this.uavFormat = format;
        }

        public void ReloadAsDSV(int width, int height, int mips, Format format)
        {
            this.width = width;
            this.height = height;
            this.mipLevels = mips;
            if (format == Format.D24_UNorm_S8_UInt)
                this.format = Format.R24_UNorm_X8_Typeless;
            else if (format == Format.D32_Float)
                this.format = Format.R32_Float;
            this.dsvFormat = format;
            this.rtvFormat = Format.Unknown;
            this.uavFormat = Format.Unknown;
        }

        internal CpuDescriptorHandle GetRenderTargetView(ID3D12Device device, int mipLevel, int faceIndex)
        {
            if (renderTargetView == null)
            {
                ThrowIfFailed(device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, 1), out renderTargetView));
                device.CreateRenderTargetView(resource, new RenderTargetViewDescription()
                {
                    Texture2DArray = new Texture2DArrayRenderTargetView()
                    {
                        MipSlice = mipLevel,
                        ArraySize = 1,
                        FirstArraySlice = faceIndex,
                    }
                }, renderTargetView.GetCPUDescriptorHandleForHeapStart());
            }
            return renderTargetView.GetCPUDescriptorHandleForHeapStart();
        }

        internal ResourceDescription GetResourceDescription()
        {
            ResourceDescription textureDesc = new ResourceDescription
            {
                MipLevels = (ushort)mipLevels,
                Width = (ulong)width,
                Height = height,
                DepthOrArraySize = 6,
                Dimension = ResourceDimension.Texture2D,
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

        public Format GetFormat()
        {
            if (dsvFormat != Format.Unknown)
                return dsvFormat;
            return format;
        }

        public void Dispose()
        {
            resource?.Release();
            resource = null;
            Status = GraphicsObjectStatus.unload;
        }
    }
}
