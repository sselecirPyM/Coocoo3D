using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics.Commanding
{
    public delegate void CBVAction(CBVProxy proxy);
    public class GraphicsCommandProxy
    {
        public GraphicsContext graphicsContext;

        public Dictionary<int, int> srvs;
        public Dictionary<int, int> cbvs;
        public Dictionary<int, int> uavs;

        public ulong UploadCBV(int slot, CBVAction action)
        {
            if (!graphicsContext.currentPSO.cbvDescriptions.TryGetValue(slot, out var desc))
                return 0;

            if (cbvs.TryGetValue(slot, out var cbv))
            {
                byte[] buffer = new byte[desc.size];
                var proxy = new CBVProxy
                {
                    positionMap = desc.positionMap,
                    buffer = buffer
                };

                action(proxy);
                graphicsContext.readonlyBufferAllocator.Upload(buffer, 256, out ulong addr);
                return addr;
            }
            return 0;
        }

        public void SetCBV(int slot, CBVAction action)
        {
            if (!graphicsContext.currentPSO.cbvDescriptions.TryGetValue(slot, out var desc))
                return;

            if (cbvs.TryGetValue(slot, out var cbv))
            {
                byte[] buffer = new byte[desc.size];
                var proxy = new CBVProxy
                {
                    positionMap = desc.positionMap,
                    buffer = buffer
                };

                action(proxy);
                graphicsContext.readonlyBufferAllocator.Upload(buffer, 256, out ulong addr);
                graphicsContext.m_commandList.SetGraphicsRootConstantBufferView(cbv, addr);
            }
        }

        public void SetCBV(int slot, ulong gpuVirtualAddress)
        {
            if (cbvs.TryGetValue(slot, out var cbv))
            {
                graphicsContext.m_commandList.SetGraphicsRootConstantBufferView(cbv, gpuVirtualAddress);
            }
        }

        public void SetCBV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
        {
            if (cbvs.TryGetValue(slot, out var cbv))
            {
                graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes(data), 256, out ulong addr);
                graphicsContext.m_commandList.SetGraphicsRootConstantBufferView(cbv, addr);
            }
        }

        public void SetCBV<T>(int slot, T[] data) where T : unmanaged
        {
            if (cbvs.TryGetValue(slot, out var cbv))
            {
                graphicsContext.readonlyBufferAllocator.Upload(MemoryMarshal.AsBytes<T>(data), 256, out ulong addr);
                graphicsContext.m_commandList.SetGraphicsRootConstantBufferView(cbv, addr);
            }
        }

        public void SetSRV(int slot, ulong gpuVirtualAddress)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                graphicsContext.m_commandList.SetGraphicsRootShaderResourceView(srv, gpuVirtualAddress);
            }
        }

        public void SetSRV(int slot, GpuDescriptorHandle handle)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, handle);
            }
        }

        public void SetSRV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                var handle = graphicsContext.readonlyBufferAllocator.GetSRV(MemoryMarshal.AsBytes(data));
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, handle);
            }
        }

        public void SetSRV(int slot, RTTopLevelAcclerationStruct tlas)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                graphicsContext.m_commandList.SetGraphicsRootShaderResourceView(srv, tlas.GPUVirtualAddress);
            }
        }

        public void SetSRV(int slot, Texture2D texture, bool linear = false)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                var handle = graphicsContext.GetSRVHandle(texture, linear);
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, handle);
            }
        }

        public void SetSRV(int slot, GPUBuffer gpuBuffer)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                var handle = graphicsContext.GetSRVHandle(gpuBuffer);
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, handle);
            }
        }

        public void SetSRV(int slot, Mesh mesh, string bufferName)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                var buffer = mesh.GetVertexBuffer(bufferName);
                ID3D12Resource resource;
                ulong firstElement = 0;
                if (buffer.baseBuffer != null)
                {
                    resource = buffer.baseBuffer.resource;
                    buffer.baseBuffer.ToState(graphicsContext.m_commandList, ResourceStates.Common);
                    firstElement = (ulong)buffer.baseBufferOffset / 4;
                }
                else
                {
                    resource = buffer.resource;
                }
                var handle = graphicsContext.CreateSRV(resource, new ShaderResourceViewDescription()
                {
                    Format = Format.R32_Typeless,
                    ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer,
                    Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                    Buffer = new BufferShaderResourceView
                    {
                        FirstElement = firstElement,
                        NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                        Flags = BufferShaderResourceViewFlags.Raw,
                    }
                });
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, handle);
            }
        }

        public void SetSRVMip(int slot, Texture2D texture, int mips)
        {
            if (srvs.TryGetValue(slot, out var srv))
            {
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(srv, graphicsContext.GetSRVHandleWithMip(texture, mips));
            }
        }

        public void SetUAV(int slot, Texture2D texture)
        {
            if (uavs.TryGetValue(slot, out var uav))
            {
                var handle = graphicsContext.GetUAVHandle(texture);
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(uav, handle);
            }
        }

        public void SetUAVMip(int slot, Texture2D texture, int mipIndex)
        {
            if (uavs.TryGetValue(slot, out var uav))
            {
                texture.SetPartResourceState(graphicsContext.m_commandList, ResourceStates.UnorderedAccess, mipIndex, 1);
                if (!(mipIndex < texture.mipLevels))
                {
                    throw new ArgumentOutOfRangeException();
                }
                var uavDesc = new UnorderedAccessViewDescription()
                {
                    Format = texture.uavFormat,
                };
                if (texture.isCube)
                {
                    uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2DArray;
                    uavDesc.Texture2DArray = new Texture2DArrayUnorderedAccessView() { MipSlice = mipIndex, ArraySize = 6 };
                }
                else
                {
                    uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2D;
                    uavDesc.Texture2D = new Texture2DUnorderedAccessView { MipSlice = mipIndex };
                }

                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(uav, graphicsContext.CreateUAV(texture.resource, uavDesc));
            }
        }

        public void SetUAV(int slot, GPUBuffer gpuBuffer)
        {
            if (uavs.TryGetValue(slot, out var uav))
            {
                var handle = graphicsContext.GetUAVHandle(gpuBuffer);
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(uav, handle);
            }
        }
        public void SetUAV(int slot, Mesh mesh, string bufferName)
        {
            if (uavs.TryGetValue(slot, out var uav))
            {
                var buffer = mesh.GetVertexBuffer(bufferName);
                buffer.baseBuffer.ToState(graphicsContext.m_commandList, ResourceStates.UnorderedAccess);

                var handle = graphicsContext.CreateUAV(buffer.baseBuffer.resource, new UnorderedAccessViewDescription()
                {
                    Format = Format.R32_Typeless,
                    ViewDimension = UnorderedAccessViewDimension.Buffer,
                    Buffer = new BufferUnorderedAccessView
                    {
                        FirstElement = (ulong)buffer.baseBufferOffset / 4,
                        NumElements = buffer.vertexBufferView.SizeInBytes / 4,
                        Flags = BufferUnorderedAccessViewFlags.Raw,
                    }
                });
                graphicsContext.m_commandList.SetGraphicsRootDescriptorTable(uav, handle);
            }
        }
    }
}
