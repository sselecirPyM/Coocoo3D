using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vortice.Direct3D12;
using Vortice.Direct3D;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;
using System.Runtime.InteropServices;
using System.IO;

namespace Coocoo3DGraphics
{
    public class GraphicsContext
    {
        const int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

        GraphicsDevice graphicsDevice;
        ID3D12GraphicsCommandList4 m_commandList;
        public RootSignature currentRootSignature;
        RootSignature _currentGraphicsRootSignature;
        RootSignature _currentComputeRootSignature;

        public Dictionary<int, object> slots = new Dictionary<int, object>();

        public RTPSO currentRTPSO;
        public PSO currentPSO;
        public PSODesc currentPSODesc;
        //public bool psoChange;

        public Dictionary<int, ulong> currentCBVs = new Dictionary<int, ulong>();
        public Dictionary<int, ulong> currentSRVs = new Dictionary<int, ulong>();
        public Dictionary<int, ulong> currentUAVs = new Dictionary<int, ulong>();

        public int TriangleCount { get; private set; }

        public void Reload(GraphicsDevice device)
        {
            this.graphicsDevice = device;
        }

        public bool SetPSO(ComputeShader computeShader)
        {
            if (!computeShader.computeShaders.TryGetValue(currentRootSignature.rootSignature, out ID3D12PipelineState pipelineState))
            {
                ComputePipelineStateDescription desc = new ComputePipelineStateDescription();
                desc.ComputeShader = computeShader.data;
                desc.RootSignature = currentRootSignature.rootSignature;
                if (graphicsDevice.device.CreateComputePipelineState(desc, out pipelineState).Failure)
                {
                    return false;
                }

                computeShader.computeShaders[currentRootSignature.rootSignature] = pipelineState;
            }

            m_commandList.SetPipelineState(pipelineState);
            InReference(pipelineState);
            return true;
        }

        public bool SetPSO(PSO pso, in PSODesc desc)
        {
            if (pso.TryGetPipelineState(graphicsDevice, currentRootSignature, desc, out var pipelineState))
            {
                m_commandList.SetPipelineState(pipelineState);
                InReference(pipelineState);
                currentPSO = pso;
                currentPSODesc = desc;
                //psoChange = true;
                return true;
            }
            return false;
        }

        public bool SetPSO(RTPSO pso)
        {
            if (!graphicsDevice.IsRayTracingSupport()) return false;
            if (pso == null) return false;

            var device = graphicsDevice.device;
            if (pso.so == null)
            {
                if (pso.exports == null || pso.exports.Length == 0) return false;

                pso.globalRootSignature?.Dispose();
                pso.globalRootSignature = new RootSignature();
                pso.globalRootSignature.ReloadCompute(pso.shaderAccessTypes);
                pso.globalRootSignature.Sign1(graphicsDevice);

                List<StateSubObject> stateSubObjects = new List<StateSubObject>();

                List<ExportDescription> exportDescriptions = new List<ExportDescription>();
                foreach (var export in pso.exports)
                    exportDescriptions.Add(new ExportDescription(export));

                stateSubObjects.Add(new StateSubObject(new DxilLibraryDescription(pso.datas, exportDescriptions.ToArray())));
                stateSubObjects.Add(new StateSubObject(new HitGroupDescription("emptyhitgroup", HitGroupType.Triangles, null, null, null)));
                foreach (var hitGroup in pso.hitGroups)
                {
                    stateSubObjects.Add(new StateSubObject(new HitGroupDescription(hitGroup.name, HitGroupType.Triangles, hitGroup.anyHit, hitGroup.closestHit, hitGroup.intersection)));
                }
                if (pso.localShaderAccessTypes != null)
                {
                    pso.localRootSignature?.Dispose();
                    pso.localRootSignature = new RootSignature();
                    pso.localRootSignature.ReloadLocalRootSignature(pso.localShaderAccessTypes);
                    pso.localRootSignature.Sign1(graphicsDevice, 1);
                    pso.localSize += pso.localShaderAccessTypes.Length * 8;
                    stateSubObjects.Add(new StateSubObject(new LocalRootSignature(pso.localRootSignature.rootSignature)));
                    string[] hitGroups = new string[pso.hitGroups.Length];
                    for (int i = 0; i < pso.hitGroups.Length; i++)
                        hitGroups[i] = pso.hitGroups[i].name;
                    stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], hitGroups)));
                }

                stateSubObjects.Add(new StateSubObject(new RaytracingShaderConfig(64, 20)));
                stateSubObjects.Add(new StateSubObject(new SubObjectToExportsAssociation(stateSubObjects[stateSubObjects.Count - 1], pso.exports)));
                stateSubObjects.Add(new StateSubObject(new RaytracingPipelineConfig(2)));
                stateSubObjects.Add(new StateSubObject(new GlobalRootSignature(pso.globalRootSignature.rootSignature)));
                var result = device.CreateStateObject(new StateObjectDescription(StateObjectType.RaytracingPipeline, stateSubObjects.ToArray()), out pso.so);
                if (result.Failure)
                    return false;
            }
            SetRootSignature(pso.globalRootSignature);
            currentRTPSO = pso;
            m_commandList.SetPipelineState1(pso.so);
            return true;
        }

        void SetRTTopAccelerationStruct(RTTopLevelAcclerationStruct accelerationStruct)
        {
            if (accelerationStruct.initialized)
                return;
            accelerationStruct.initialized = true;
            if (graphicsDevice.scratchResource == null)
            {
                CreateUAVBuffer(134217728, ref graphicsDevice.scratchResource, ResourceStates.UnorderedAccess);
            }
            int instanceCount = accelerationStruct.instances.Count;
            RaytracingInstanceDescription[] raytracingInstanceDescriptions = new RaytracingInstanceDescription[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                RTInstance instance = accelerationStruct.instances[i];
                var btas = instance.accelerationStruct;
                var mesh = btas.mesh;
                var meshOverride = btas.meshOverride;
                if (btas.initialized)
                    continue;

                ulong pos;
                if (meshOverride != null && meshOverride.vtBuffers.TryGetValue(0, out var v0))
                    pos = v0.vertex.GPUVirtualAddress + (ulong)btas.vertexStart * 12;
                else
                    pos = mesh.vtBuffers[0].vertex.GPUVirtualAddress + (ulong)btas.vertexStart * 12;
                BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
                inputs.Type = RaytracingAccelerationStructureType.BottomLevel;
                inputs.Layout = ElementsLayout.Array;
                inputs.GeometryDescriptions = new RaytracingGeometryDescription[]
                {
                    new RaytracingGeometryDescription(new RaytracingGeometryTrianglesDescription(new GpuVirtualAddressAndStride(pos, 12),
                    Format.R32G32B32_Float,
                    btas.vertexCount,
                    0,
                    mesh.indexBuffer.GPUVirtualAddress + (ulong)btas.indexStart * 4,
                    Format.R32_UInt,
                    btas.indexCount)),
                };
                InReference(mesh.vtBuffers[0].vertex);
                InReference(mesh.indexBuffer);
                inputs.DescriptorsCount = 1;
                RaytracingAccelerationStructurePrebuildInfo info = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

                CreateUAVBuffer((int)info.ResultDataMaxSizeInBytes, ref btas.resource, ResourceStates.RaytracingAccelerationStructure);
                BuildRaytracingAccelerationStructureDescription brtas = new BuildRaytracingAccelerationStructureDescription();
                brtas.Inputs = inputs;
                brtas.ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress;
                brtas.DestinationAccelerationStructureData = btas.resource.GPUVirtualAddress;

                m_commandList.BuildRaytracingAccelerationStructure(brtas);
                m_commandList.ResourceBarrierUnorderedAccessView(btas.resource);
                InReference(btas.resource);
                RaytracingInstanceDescription raytracingInstanceDescription = new RaytracingInstanceDescription();
                raytracingInstanceDescription.AccelerationStructure = (long)btas.resource.GPUVirtualAddress;
                raytracingInstanceDescription.InstanceContributionToHitGroupIndex = (Vortice.UInt24)(uint)i;
                raytracingInstanceDescription.InstanceID = (Vortice.UInt24)(uint)i;
                raytracingInstanceDescription.InstanceMask = instance.instanceMask;
                raytracingInstanceDescription.Transform = GetMatrix3X4(Matrix4x4.Transpose(instance.transform));
                raytracingInstanceDescriptions[i] = raytracingInstanceDescription;
                btas.initialized = true;
            }
            GetRingBuffer().Upload(raytracingInstanceDescriptions, out ulong gpuAddr);
            BuildRaytracingAccelerationStructureInputs tpInputs = new BuildRaytracingAccelerationStructureInputs();
            tpInputs.Layout = ElementsLayout.Array;
            tpInputs.Type = RaytracingAccelerationStructureType.TopLevel;
            tpInputs.DescriptorsCount = accelerationStruct.instances.Count;
            tpInputs.InstanceDescriptions = (long)gpuAddr;

            RaytracingAccelerationStructurePrebuildInfo info1 = graphicsDevice.device.GetRaytracingAccelerationStructurePrebuildInfo(tpInputs);
            CreateUAVBuffer((int)info1.ResultDataMaxSizeInBytes, ref accelerationStruct.resource, ResourceStates.RaytracingAccelerationStructure);
            InReference(accelerationStruct.resource);
            BuildRaytracingAccelerationStructureDescription trtas = new BuildRaytracingAccelerationStructureDescription();
            trtas.Inputs = tpInputs;
            trtas.DestinationAccelerationStructureData = accelerationStruct.resource.GPUVirtualAddress;
            trtas.ScratchAccelerationStructureData = graphicsDevice.scratchResource.GPUVirtualAddress;
            m_commandList.BuildRaytracingAccelerationStructure(trtas);
        }

        public unsafe void DispatchRays(int width, int height, int depth, RayTracingCall call)
        {
            SetRTTopAccelerationStruct(call.tpas);
            const int D3D12ShaderIdentifierSizeInBytes = 32;
            ID3D12StateObjectProperties pRtsoProps = currentRTPSO.so.QueryInterface<ID3D12StateObjectProperties>();
            InReference(currentRTPSO.so);
            DispatchRaysDescription dispatchRaysDescription = new DispatchRaysDescription();
            dispatchRaysDescription.Width = width;
            dispatchRaysDescription.Height = height;
            dispatchRaysDescription.Depth = depth;
            dispatchRaysDescription.HitGroupTable = new GpuVirtualAddressRangeAndStride();
            dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride();

            currentRootSignature = currentRTPSO.globalRootSignature;
            SetSRVRSlot(call.tpas.resource.GPUVirtualAddress, 0);

            {
                int cbvOffset = 0;
                int srvOffset = 0;
                int uavOffset = 0;
                foreach (var access in currentRTPSO.shaderAccessTypes)
                {
                    if (access == ResourceAccessType.SRV)
                    {
                        srvOffset++;
                    }
                    else if (access == ResourceAccessType.SRVTable)
                    {
                        if (call.SRVs != null && call.SRVs.TryGetValue(srvOffset, out object srv0))
                        {
                            if (srv0 is Texture2D tex2d)
                            {
                                if (!call.srvFlags.ContainsKey(srvOffset))
                                    SetSRVTSlot(tex2d, srvOffset);
                                else
                                    SetSRVTSlotLinear(tex2d, srvOffset);
                            }
                            else if (srv0 is TextureCube texCube)
                                SetSRVTSlot(texCube, srvOffset);
                            else if (srv0 is GPUBuffer buffer)
                                SetSRVTSlot(buffer, srvOffset);
                        }

                        srvOffset++;
                    }
                    else if (access == ResourceAccessType.CBV)
                    {
                        if (call.CBVs != null && call.CBVs.TryGetValue(cbvOffset, out object cbv0))
                        {
                            if (cbv0 is byte[] cbvData)
                                SetCBVRSlot<byte>(cbvData, cbvOffset);
                            else if (cbv0 is Matrix4x4[] cbvDataM)
                                SetCBVRSlot<Matrix4x4>(cbvDataM, cbvOffset);
                            else if (cbv0 is Vector4[] cbvDataF4)
                                SetCBVRSlot<Vector4>(cbvDataF4, cbvOffset);
                        }

                        cbvOffset++;
                    }
                    else if (access == ResourceAccessType.UAVTable)
                    {
                        if (call.UAVs != null && call.UAVs.TryGetValue(uavOffset, out object uav0))
                        {
                            if (uav0 is Texture2D tex2d)
                                SetRTSlot(tex2d, uavOffset);
                            else if (uav0 is GPUBuffer buffer)
                                SetUAVTSlot(buffer, uavOffset);
                        }
                        uavOffset++;
                    }
                }
            }

            byte[] data = new byte[32];
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(memoryStream);
            memcpy(data, pRtsoProps.GetShaderIdentifier(call.rayGenShader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
            writer.Write(data);
            ulong gpuaddr;
            int length1 = (int)memoryStream.Position;
            GetRingBuffer().Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
            dispatchRaysDescription.RayGenerationShaderRecord = new GpuVirtualAddressRange(gpuaddr, (ulong)length1);
            writer.Seek(0, SeekOrigin.Begin);

            foreach (var inst in call.tpas.instances)
            {
                if (inst.hitGroupName != null)
                {
                    var mesh = inst.accelerationStruct.mesh;
                    var meshOverride = inst.accelerationStruct.meshOverride;
                    memcpy(data, pRtsoProps.GetShaderIdentifier(inst.hitGroupName).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    int vertexStart = inst.accelerationStruct.vertexStart;
                    writer.Write(data);
                    writer.Write(mesh.indexBuffer.GPUVirtualAddress + (ulong)inst.accelerationStruct.indexStart * 4);
                    for (int i = 0; i < 3; i++)
                    {
                        if (meshOverride != null && meshOverride.vtBuffers.TryGetValue(i, out var meshX1))
                        {
                            writer.Write(meshX1.vertex.GPUVirtualAddress + (ulong)(meshX1.vertexBufferView.StrideInBytes * vertexStart));
                        }
                        else
                            writer.Write(mesh.vtBuffers[i].vertex.GPUVirtualAddress + (ulong)(mesh.vtBuffers[i].vertexBufferView.StrideInBytes * vertexStart));
                    }
                    int cbvOffset = 0;
                    int srvOffset = 0;
                    foreach (var access in currentRTPSO.localShaderAccessTypes)
                    {
                        if (access == ResourceAccessType.CBV)
                        {
                            if (inst.CBVs != null && inst.CBVs.TryGetValue(cbvOffset, out object cbv0))
                            {
                                if (cbv0 is byte[] cbvData)
                                    _RTWriteGpuAddr<byte>(cbvData, writer);
                                else if (cbv0 is Matrix4x4[] cbvDataM)
                                    _RTWriteGpuAddr<Matrix4x4>(cbvDataM, writer);
                                else if (cbv0 is Vector4[] cbvDataF4)
                                    _RTWriteGpuAddr<Vector4>(cbvDataF4, writer);
                                else
                                    writer.Write((ulong)0);
                            }
                            else
                                writer.Write((ulong)0);
                            cbvOffset++;
                        }
                        else if (access == ResourceAccessType.SRV)
                        {
                            srvOffset++;
                        }
                        else if (access == ResourceAccessType.SRVTable)
                        {
                            if (inst.SRVs != null && inst.SRVs.TryGetValue(srvOffset, out object srv0))
                            {
                                if (srv0 is Texture2D tex2d)
                                    writer.Write(GetSRVHandle(tex2d).Ptr);
                                else if (srv0 is GPUBuffer buffer)
                                    writer.Write(GetSRVHandle(buffer).Ptr);
                                else if (srv0 is ID3D12Resource resource)
                                    writer.Write(InReferenceAddr(resource));
                                else
                                    writer.Write((ulong)0);
                            }
                            else
                                writer.Write((ulong)0);
                            srvOffset++;
                        }
                    }
                    var newPos = align_to(64, (int)memoryStream.Position) - (int)memoryStream.Position;
                    for (int k = 0; k < newPos; k++)
                    {
                        writer.Write((byte)0);
                    }
                }
                else
                {
                    memcpy(data, pRtsoProps.GetShaderIdentifier("emptyhitgroup").ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    writer.Write(data);
                    for (int i = 0; i < currentRTPSO.localSize - D3D12ShaderIdentifierSizeInBytes; i++)
                    {
                        writer.Write((byte)0);
                    }
                    var newPos = align_to(64, (int)memoryStream.Position) - (int)memoryStream.Position;
                    for (int k = 0; k < newPos; k++)
                    {
                        writer.Write((byte)0);
                    }
                }
            }
            if (memoryStream.Position > 0)
            {
                length1 = (int)memoryStream.Position;
                GetRingBuffer().Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
                dispatchRaysDescription.HitGroupTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, (ulong)(length1 / call.tpas.instances.Count));
            }
            writer.Seek(0, SeekOrigin.Begin);

            if (call.missShaders != null && call.missShaders.Length > 0)
            {
                foreach (var missShader in call.missShaders)
                {
                    memcpy(data, pRtsoProps.GetShaderIdentifier(missShader).ToPointer(), D3D12ShaderIdentifierSizeInBytes);
                    writer.Write(data);
                }

                length1 = (int)memoryStream.Position;
                GetRingBuffer().Upload(new Span<byte>(memoryStream.GetBuffer(), 0, length1), out gpuaddr);
                dispatchRaysDescription.MissShaderTable = new GpuVirtualAddressRangeAndStride(gpuaddr, (ulong)length1, (ulong)(length1 / call.missShaders.Length));
            }
            writer.Seek(0, SeekOrigin.Begin);

            pRtsoProps.Dispose();
            PipelineBindingCompute();
            m_commandList.DispatchRays(dispatchRaysDescription);
        }

        public void SetInputLayout(UnnamedInputLayout inputLayout)
        {
            currentInputLayout = inputLayout;
        }

        public void SetSRVTSlotLinear(Texture2D texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture, true).Ptr;

        public void SetSRVTSlot(Texture2D texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture).Ptr;

        public void SetSRVTSlot(TextureCube texture, int slot) => currentSRVs[slot] = GetSRVHandle(texture).Ptr;

        public void SetSRVTSlot(GPUBuffer buffer, int slot) => currentSRVs[slot] = GetSRVHandle(buffer).Ptr;

        public void SetSRVTLim(TextureCube texture, int mips, int slot) => currentSRVs[slot] = GetSRVHandleWithMip(texture, mips).Ptr;

        void SetSRVRSlot(ulong gpuAddr, int slot) => currentSRVs[slot] = gpuAddr;

        public void SetCBVRSlot(CBuffer buffer, int offset256, int size256, int slot) => currentCBVs[slot] = buffer.GetCurrentVirtualAddress() + (ulong)(offset256 * 256);

        public void SetCBVRSlot<T>(Span<T> data, int slot) where T : unmanaged
        {
            GetRingBuffer().Upload(data, out ulong addr);
            currentCBVs[slot] = addr;
        }

        public void SetRTSlot(Texture2D texture2D, int slot) => currentUAVs[slot] = GetUAVHandle(texture2D,ResourceStates.NonPixelShaderResource).Ptr;
        public void SetUAVTSlot(Texture2D texture2D, int slot) => currentUAVs[slot] = GetUAVHandle(texture2D).Ptr;
        public void SetUAVTSlot(TextureCube textureCube, int slot) => currentUAVs[slot] = GetUAVHandle(textureCube).Ptr;
        public void SetUAVTSlot(GPUBuffer buffer, int slot) => currentUAVs[slot] = GetUAVHandle(buffer).Ptr;

        public void SetUAVTSlot(TextureCube texture, int mipIndex, int slot)
        {
            texture.SetPartResourceState(m_commandList, ResourceStates.UnorderedAccess, mipIndex, 1);
            if (!(mipIndex < texture.mipLevels))
            {
                throw new ArgumentOutOfRangeException();
            }
            var uavDesc = new UnorderedAccessViewDescription()
            {
                ViewDimension = UnorderedAccessViewDimension.Texture2DArray,
                Texture2DArray = new Texture2DArrayUnorderedAccessView() { MipSlice = mipIndex, ArraySize = 6 },
                Format = texture.uavFormat,
            };

            currentUAVs[slot] = CreateUAV(texture.resource, uavDesc).Ptr;
        }

        public unsafe void UpdateCBStaticResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.GenericRead, ResourceStates.CopyDestination);

            GetRingBuffer().Upload<T>(m_commandList, data, buffer.resource);

            commandList.ResourceBarrierTransition(buffer.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
        }

        public void UpdateCBResource<T>(CBuffer buffer, ID3D12GraphicsCommandList commandList, Span<T> data) where T : unmanaged
        {
            GetRingBuffer().Upload(data, out buffer.gpuRefAddress);
        }

        unsafe public void UploadMesh(Mesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffers)
            {
                var mesh1 = vtBuf.Value;
                int dataLength = mesh1.data.Length;
                int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.actualLength >= dataLength && u.actualLength <= dataLength * 2 + 256);
                if (index1 != -1)
                {
                    mesh1.vertex = mesh.vtBuffersDisposed[index1].vertex;
                    mesh1.actualLength = mesh.vtBuffersDisposed[index1].actualLength;
                    m_commandList.ResourceBarrierTransition(mesh1.vertex, ResourceStates.GenericRead, ResourceStates.CopyDestination);

                    mesh.vtBuffersDisposed.RemoveAt(index1);
                }
                else
                {
                    CreateBuffer(dataLength + 256, ref mesh1.vertex);
                    mesh1.actualLength = dataLength + 256;
                }

                mesh1.vertex.Name = "vertex buffer" + vtBuf.Key;

                GetRingBuffer().Upload<byte>(m_commandList, mesh1.data, mesh1.vertex);
                m_commandList.ResourceBarrierTransition(mesh1.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                InReference(mesh1.vertex);

                mesh1.vertexBufferView.BufferLocation = mesh1.vertex.GPUVirtualAddress;
                mesh1.vertexBufferView.StrideInBytes = dataLength / mesh.m_vertexCount;
                mesh1.vertexBufferView.SizeInBytes = dataLength;
            }

            foreach (var vtBuf in mesh.vtBuffersDisposed)
                vtBuf.vertex.Release();
            mesh.vtBuffersDisposed.Clear();

            if (mesh.m_indexCount > 0)
            {
                int indexBufferLength = mesh.m_indexCount * 4;
                ref var indexBuffer = ref mesh.indexBuffer;
                if (mesh.indexBufferCapacity < indexBufferLength)
                {
                    CreateBuffer(indexBufferLength, ref indexBuffer);
                    mesh.indexBufferCapacity = indexBufferLength;
                    indexBuffer.Name = "index buffer";
                }
                else
                {
                    m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.GenericRead, ResourceStates.CopyDestination);
                }
                GetRingBuffer().Upload<byte>(m_commandList, new Span<byte>(mesh.m_indexData, 0, indexBufferLength), indexBuffer);

                m_commandList.ResourceBarrierTransition(indexBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead);
                InReference(indexBuffer);
                mesh.indexBufferView.BufferLocation = indexBuffer.GPUVirtualAddress;
                mesh.indexBufferView.SizeInBytes = indexBufferLength;
                mesh.indexBufferView.Format = Format.R32_UInt;
            }
        }

        public void BeginUpdateMesh(Mesh mesh)
        {

        }

        unsafe public void UpdateMesh<T>(Mesh mesh, Span<T> data, int slot) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T));
            int sizeInBytes = data.Length * size1;

            if (!mesh.vtBuffers.TryGetValue(slot, out var vtBuf))
            {
                vtBuf = mesh.AddBuffer(slot);
            }


            int index1 = mesh.vtBuffersDisposed.FindIndex(u => u.actualLength == sizeInBytes);
            if (index1 != -1)
            {
                vtBuf.vertex = mesh.vtBuffersDisposed[index1].vertex;
                vtBuf.actualLength = mesh.vtBuffersDisposed[index1].actualLength;
                m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.GenericRead, ResourceStates.CopyDestination);

                mesh.vtBuffersDisposed.RemoveAt(index1);
            }
            else
            {
                CreateBuffer(sizeInBytes, ref vtBuf.vertex);
                vtBuf.actualLength = sizeInBytes;
            }

            vtBuf.vertex.Name = "vertex buffer" + slot;

            GetRingBuffer().Upload(m_commandList, data, vtBuf.vertex);

            m_commandList.ResourceBarrierTransition(vtBuf.vertex, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            InReference(vtBuf.vertex);
            vtBuf.vertexBufferView.BufferLocation = vtBuf.vertex.GPUVirtualAddress;
            vtBuf.vertexBufferView.StrideInBytes = sizeInBytes / mesh.m_vertexCount;
            vtBuf.vertexBufferView.SizeInBytes = sizeInBytes;
        }

        public void EndUpdateMesh(Mesh mesh)
        {
            foreach (var vtBuf in mesh.vtBuffersDisposed)
                vtBuf.vertex.Release();
            mesh.vtBuffersDisposed.Clear();
        }

        public void UpdateResource<T>(CBuffer buffer, T[] data, int sizeInByte, int dataOffset) where T : unmanaged
        {
            int size1 = Marshal.SizeOf(typeof(T));
            UpdateResource(buffer, new Span<T>(data, dataOffset, sizeInByte / size1));
        }
        public void UpdateResource<T>(CBuffer buffer, Span<T> data) where T : unmanaged
        {
            if (buffer.Mutable)
                UpdateCBResource(buffer, m_commandList, data);
            else
                UpdateCBStaticResource(buffer, m_commandList, data);
        }

        public unsafe void UploadTexture(Texture2D texture, Uploader uploader)
        {
            texture.width = uploader.m_width;
            texture.height = uploader.m_height;
            texture.mipLevels = uploader.m_mipLevels;
            texture.format = uploader.m_format;

            var textureDesc = texture.GetResourceDescription();

            CreateResource(textureDesc, null, ref texture.resource);

            texture.resource.Name = texture.Name ?? "tex2d";
            ID3D12Resource uploadBuffer = null;
            CreateBuffer(uploader.m_data.Length, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
            uploadBuffer.Name = "uploadbuffer tex";
            graphicsDevice.ResourceDelayRecycle(uploadBuffer);

            Span<SubresourceData> subresources = stackalloc SubresourceData[textureDesc.MipLevels];

            IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(uploader.m_data, 0);
            int bitsPerPixel = (int)BitsPerPixel(textureDesc.Format);
            int width = (int)textureDesc.Width;
            int height = textureDesc.Height;
            for (int i = 0; i < textureDesc.MipLevels; i++)
            {
                SubresourceData subresourcedata = new SubresourceData();
                subresourcedata.DataPointer = pdata;
                subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
                subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
                pdata += width * height * bitsPerPixel / 8;

                subresources[i] = subresourcedata;
                width /= 2;
                height /= 2;
            }

            UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, textureDesc.MipLevels, subresources);

            m_commandList.ResourceBarrierTransition(texture.resource, ResourceStates.CopyDestination, ResourceStates.GenericRead);
            InReference(texture.resource);
            texture.resourceStates = ResourceStates.GenericRead;

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UploadTexture(Texture2D texture, byte[] data)
        {
            int bitsPerPixel = (int)BitsPerPixel(texture.format);
            int width = (int)texture.width;
            int height = texture.height;

            ID3D12Resource uploadBuffer = null;
            CreateBuffer(align_to(64, width) * align_to(64, height) * bitsPerPixel / 8, ref uploadBuffer, ResourceStates.GenericRead, HeapType.Upload);
            uploadBuffer.Name = "uploadbuffer tex";
            graphicsDevice.ResourceDelayRecycle(uploadBuffer);

            Span<SubresourceData> subresources = stackalloc SubresourceData[texture.mipLevels];
            IntPtr pdata = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
            for (int i = 0; i < texture.mipLevels; i++)
            {
                SubresourceData subresourcedata = new SubresourceData();
                subresourcedata.DataPointer = pdata;
                subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
                subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
                pdata += width * height * bitsPerPixel / 8;

                subresources[i] = subresourcedata;
                width /= 2;
                height /= 2;
            }
            texture.StateChange(m_commandList, ResourceStates.CopyDestination);
            UpdateSubresources(m_commandList, texture.resource, uploadBuffer, 0, 0, texture.mipLevels, subresources);
            texture.StateChange(m_commandList, ResourceStates.GenericRead);
            InReference(texture.resource);
        }

        public void UpdateRenderTexture(Texture2D texture)
        {
            var textureDesc = texture.GetResourceDescription();

            ClearValue clearValue = texture.dsvFormat != Format.Unknown
                ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
                : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
            CreateResource(textureDesc, clearValue, ref texture.resource, ResourceStates.GenericRead);
            texture.resourceStates = ResourceStates.GenericRead;
            texture.resource.Name = "render tex2D";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateRenderTexture(TextureCube texture)
        {
            var textureDesc = texture.GetResourceDescription();

            ClearValue clearValue = texture.dsvFormat != Format.Unknown
                ? new ClearValue(texture.dsvFormat, new DepthStencilValue(1.0f, 0))
                : new ClearValue(texture.format, new Vortice.Mathematics.Color4());
            CreateResource(textureDesc, clearValue, ref texture.resource, ResourceStates.GenericRead);
            texture.InitResourceState(ResourceStates.GenericRead);
            texture.resource.Name = "render texCube";

            texture.Status = GraphicsObjectStatus.loaded;
        }

        public void UpdateDynamicBuffer(GPUBuffer buffer)
        {
            CreateUAVBuffer(buffer.size, ref buffer.resource);
            if (buffer.Name != null)
                buffer.resource.Name = buffer.Name;
            buffer.resourceStates = ResourceStates.UnorderedAccess;
        }

        public void UpdateReadBackTexture(ReadBackTexture2D texture)
        {
            if (texture.m_textureReadBack != null)
                foreach (var tex in texture.m_textureReadBack)
                    graphicsDevice.ResourceDelayRecycle(tex);
            if (texture.m_textureReadBack == null)
                texture.m_textureReadBack = new ID3D12Resource[3];
            for (int i = 0; i < texture.m_textureReadBack.Length; i++)
            {
                CreateBuffer(texture.m_width * texture.m_height * texture.bytesPerPixel, ref texture.m_textureReadBack[i], heapType: HeapType.Readback);
                texture.m_textureReadBack[i].Name = "texture readback";
            }
        }

        public void SetMesh(Mesh mesh)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
            {
                m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                InReference(vtBuf.Value.vertex);
            }
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            InReference(mesh.indexBuffer);
        }

        public void SetMesh(Mesh mesh, Mesh meshOverride)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            foreach (var vtBuf in mesh.vtBuffers)
            {
                if (!meshOverride.vtBuffers.ContainsKey(vtBuf.Key))
                {
                    m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                    InReference(vtBuf.Value.vertex);
                }
            }
            foreach (var vtBuf in meshOverride.vtBuffers)
            {
                m_commandList.IASetVertexBuffers(vtBuf.Key, vtBuf.Value.vertexBufferView);
                InReference(vtBuf.Value.vertex);
            }
            m_commandList.IASetIndexBuffer(mesh.indexBufferView);
            InReference(mesh.indexBuffer);
        }

        public void SetMesh(GPUBuffer mesh, Span<byte> vertexData, Span<byte> indexData, int vertexCount, int indexCount)
        {
            m_commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            int alignedVertSize = align_to(vertexData.Length, 256);
            int alignedIdxSize = align_to(indexData.Length, 256);
            int totalSize = alignedVertSize + alignedIdxSize;
            if (mesh.size < totalSize)
            {
                int newSize = totalSize + 65536;
                CreateBuffer(newSize, ref mesh.resource, ResourceStates.CopyDestination);
                mesh.resourceStates = ResourceStates.CopyDestination;
                mesh.size = newSize;
            }
            mesh.StateChange(m_commandList, ResourceStates.CopyDestination);
            GetRingBuffer().Upload<byte>(m_commandList, vertexData, mesh.resource, 0);
            GetRingBuffer().Upload<byte>(m_commandList, indexData, mesh.resource, alignedVertSize);
            mesh.StateChange(m_commandList, ResourceStates.GenericRead);

            VertexBufferView vertexBufferView;
            vertexBufferView.BufferLocation = mesh.resource.GPUVirtualAddress;
            vertexBufferView.SizeInBytes = vertexData.Length;
            vertexBufferView.StrideInBytes = vertexData.Length / vertexCount;

            IndexBufferView indexBufferView;
            indexBufferView.BufferLocation = mesh.resource.GPUVirtualAddress + (ulong)alignedVertSize;
            indexBufferView.SizeInBytes = indexData.Length;
            indexBufferView.Format = (indexData.Length / indexCount) == 2 ? Format.R16_UInt : Format.R32_UInt;

            m_commandList.IASetVertexBuffers(0, vertexBufferView);
            m_commandList.IASetIndexBuffer(indexBufferView);
            InReference(mesh.resource);
        }

        public void CopyTexture(Texture2D target, Texture2D source)
        {
            target.StateChange(m_commandList, ResourceStates.GenericRead);
            source.StateChange(m_commandList, ResourceStates.CopyDestination);
            m_commandList.CopyResource(target.resource, source.resource);
        }

        public void CopyTexture(ReadBackTexture2D target, Texture2D texture2D, int index)
        {
            var backBuffer = texture2D.resource;
            texture2D.StateChange(m_commandList, ResourceStates.CopySource);

            PlacedSubresourceFootPrint footPrint = new PlacedSubresourceFootPrint();
            footPrint.Footprint.Width = target.m_width;
            footPrint.Footprint.Height = target.m_height;
            footPrint.Footprint.Depth = 1;
            footPrint.Footprint.RowPitch = (target.m_width * 4 + 255) & ~255;
            footPrint.Footprint.Format = texture2D.format;
            TextureCopyLocation Dst = new TextureCopyLocation(target.m_textureReadBack[index], footPrint);
            TextureCopyLocation Src = new TextureCopyLocation(backBuffer, 0);
            m_commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
        }

        public void RSSetScissorRect(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
        }
        public void RSSetScissorRectAndViewport(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
            m_commandList.RSSetViewport(left, top, right - left, bottom - top);
        }

        public void Begin()
        {
            m_commandList = graphicsDevice.GetCommandList();
            m_commandList.Reset(graphicsDevice.GetCommandAllocator());
            m_commandList.SetDescriptorHeaps(1, new ID3D12DescriptorHeap[] { graphicsDevice.cbvsrvuavHeap.heap });
            ClearState();
            TriangleCount = 0;
        }

        public void ClearState()
        {
            currentPSO = null;
            currentRTPSO = null;
            _currentGraphicsRootSignature = null;
            _currentComputeRootSignature = null;
            currentRootSignature = null;
            currentInputLayout = null;
            currentCBVs.Clear();
            currentSRVs.Clear();
            currentUAVs.Clear();
        }

        public void ClearSwapChain(SwapChain swapChain, Vector4 color)
        {
            var handle1 = graphicsDevice.GetRenderTargetView(swapChain.GetResource(m_commandList));
            m_commandList.ClearRenderTargetView(handle1, new Vortice.Mathematics.Color(color));
        }

        public void SetRTV(Texture2D RTV, Vector4 color, bool clear) => SetRTVDSV(RTV, null, color, clear, false);

        public void SetRTV(IReadOnlyList<Texture2D> RTVs, Vector4 color, bool clear) => SetRTVDSV(RTVs, null, color, clear, false);

        public void SetDSV(Texture2D texture, bool clear)
        {
            m_commandList.RSSetScissorRect(texture.width, texture.height);
            m_commandList.RSSetViewport(0, 0, texture.width, texture.height);
            texture.StateChange(m_commandList, ResourceStates.DepthWrite);
            var dsv = graphicsDevice.GetDepthStencilView(texture.resource);
            InReference(texture.resource);
            if (clear)
                m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            m_commandList.OMSetRenderTargets(new CpuDescriptorHandle[0], dsv);
        }

        public void SetRTVDSV(Texture2D RTV, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.StateChange(m_commandList, ResourceStates.RenderTarget);
            var rtv = graphicsDevice.GetRenderTargetView(RTV.resource);
            InReference(RTV.resource);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = graphicsDevice.GetDepthStencilView(DSV.resource);
                InReference(DSV.resource);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
                m_commandList.OMSetRenderTargets(rtv, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(rtv);
            }
        }

        public void SetRTVDSV(TextureCube RTV, Texture2D DSV, Vector4 color, int faceIndex, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTV.width, RTV.height);
            m_commandList.RSSetViewport(0, 0, RTV.width, RTV.height);
            RTV.SetResourceState(m_commandList, ResourceStates.RenderTarget, 0, faceIndex);
            var rtv = RTV.GetRenderTargetView(graphicsDevice.device, 0, faceIndex);
            InReference(RTV.renderTargetView);
            InReference(RTV.resource);
            if (clearRTV)
                m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = graphicsDevice.GetDepthStencilView(DSV.resource);
                InReference(DSV.resource);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
                m_commandList.OMSetRenderTargets(rtv, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(rtv);
            }
        }

        public void SetRTVDSV(IReadOnlyList<Texture2D> RTVs, Texture2D DSV, Vector4 color, bool clearRTV, bool clearDSV)
        {
            m_commandList.RSSetScissorRect(RTVs[0].width, RTVs[0].height);
            m_commandList.RSSetViewport(0, 0, RTVs[0].width, RTVs[0].height);

            CpuDescriptorHandle[] handles = new CpuDescriptorHandle[RTVs.Count];
            for (int i = 0; i < RTVs.Count; i++)
            {
                RTVs[i].StateChange(m_commandList, ResourceStates.RenderTarget);
                handles[i] = graphicsDevice.GetRenderTargetView(RTVs[i].resource);
                InReference(RTVs[i].resource);
                if (clearRTV)
                    m_commandList.ClearRenderTargetView(handles[i], new Vortice.Mathematics.Color4(color));
            }
            if (DSV != null)
            {
                DSV.StateChange(m_commandList, ResourceStates.DepthWrite);
                var dsv = graphicsDevice.GetDepthStencilView(DSV.resource);
                InReference(DSV.resource);
                if (clearDSV)
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);

                m_commandList.OMSetRenderTargets(handles, dsv);
            }
            else
            {
                m_commandList.OMSetRenderTargets(handles);
            }
        }

        public void ClearTexture(Texture2D texture, Vector4 color, float depth)
        {
            switch (texture.GetFormat())
            {
                case Format.D16_UNorm:
                case Format.D24_UNorm_S8_UInt:
                case Format.D32_Float:
                case Format.D32_Float_S8X24_UInt:
                    texture.StateChange(m_commandList, ResourceStates.DepthWrite);
                    var dsv = graphicsDevice.GetDepthStencilView(texture.resource);
                    m_commandList.ClearDepthStencilView(dsv, ClearFlags.Depth | ClearFlags.Stencil, depth, 0);
                    break;
                default:
                    texture.StateChange(m_commandList, ResourceStates.RenderTarget);
                    var rtv = graphicsDevice.GetRenderTargetView(texture.resource);
                    m_commandList.ClearRenderTargetView(rtv, new Vortice.Mathematics.Color4(color));
                    break;
            }
            InReference(texture.resource);
        }

        public void SetRootSignature(RootSignature rootSignature)
        {
            ClearState();
            this.currentRootSignature = rootSignature;
            rootSignature.GetRootSignature(graphicsDevice);
        }

        public void SetRenderTargetSwapChain(SwapChain swapChain, Vector4 color, bool clear)
        {
            m_commandList.RSSetScissorRect(swapChain.width, swapChain.height);
            m_commandList.RSSetViewport(0, 0, swapChain.width, swapChain.height);
            var renderTargetView = graphicsDevice.GetRenderTargetView(swapChain.GetResource(m_commandList));
            if (clear)
                m_commandList.ClearRenderTargetView(renderTargetView, new Vortice.Mathematics.Color4(color));
            m_commandList.OMSetRenderTargets(renderTargetView);
        }

        public void Draw(int vertexCount, int startVertexLocation)
        {
            PipelineBinding();
            m_commandList.DrawInstanced(vertexCount, 1, startVertexLocation, 0);
            TriangleCount += vertexCount / 3;
        }

        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            DrawIndexedInstanced(indexCount, 1, startIndexLocation, baseVertexLocation, 0);
        }

        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            PipelineBinding();
            m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
            TriangleCount += indexCountPerInstance / 3 * instanceCount;
        }

        void PipelineBindingCompute()
        {
            if (_currentComputeRootSignature != currentRootSignature)
            {
                _currentComputeRootSignature = currentRootSignature;
                _currentGraphicsRootSignature = null;
                m_commandList.SetComputeRootSignature(currentRootSignature.rootSignature);
            }
            int cbvOffset = 0;
            int srvOffset = 0;
            int uavOffset = 0;
            for (int i = 0; i < currentRootSignature.descs.Length; i++)
            {
                ResourceAccessType d = currentRootSignature.descs[i];
                if (d == ResourceAccessType.CBV)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetComputeRootConstantBufferView(i, addr);
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.CBVTable)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.SRV)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetComputeRootShaderResourceView(i, addr);
                    srvOffset++;
                }
                else if (d == ResourceAccessType.SRVTable)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    srvOffset++;
                }
                else if (d == ResourceAccessType.UAV)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetComputeRootUnorderedAccessView(i, addr);
                    uavOffset++;
                }
                else if (d == ResourceAccessType.UAVTable)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetComputeRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    uavOffset++;
                }
            }
        }

        void PipelineBinding()
        {
            if (_currentGraphicsRootSignature != currentRootSignature)
            {
                _currentGraphicsRootSignature = currentRootSignature;
                _currentComputeRootSignature = null;
                m_commandList.SetGraphicsRootSignature(currentRootSignature.rootSignature);
            }
            int cbvOffset = 0;
            int srvOffset = 0;
            int uavOffset = 0;
            for (int i = 0; i < currentRootSignature.descs.Length; i++)
            {
                ResourceAccessType d = currentRootSignature.descs[i];
                if (d == ResourceAccessType.CBV)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootConstantBufferView(i, addr);
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.CBVTable)
                {
                    if (currentCBVs.TryGetValue(cbvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    cbvOffset++;
                }
                else if (d == ResourceAccessType.SRV)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootShaderResourceView(i, addr);
                    srvOffset++;
                }
                else if (d == ResourceAccessType.SRVTable)
                {
                    if (currentSRVs.TryGetValue(srvOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    srvOffset++;
                }
                else if (d == ResourceAccessType.UAV)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetGraphicsRootUnorderedAccessView(i, addr);
                    uavOffset++;
                }
                else if (d == ResourceAccessType.UAVTable)
                {
                    if (currentUAVs.TryGetValue(uavOffset, out ulong addr))
                        m_commandList.SetGraphicsRootDescriptorTable(i, new GpuDescriptorHandle() { Ptr = addr });
                    uavOffset++;
                }
            }
            //if (psoChange)
            //{
            //    psoChange = false;

            //    int variantIndex = currentPSO.GetVariantIndex(graphicsDevice, currentRootSignature, currentPSODesc);
            //    if (variantIndex != -1)
            //    {
            //        m_commandList.SetPipelineState(currentPSO.m_pipelineStates[variantIndex]);
            //        InReference(currentPSO.m_pipelineStates[variantIndex]);
            //    }
            //}
        }

        public void Dispatch(int x, int y, int z)
        {
            PipelineBindingCompute();
            m_commandList.Dispatch(x, y, z);
        }

        public void EndCommand()
        {
            foreach (var pair in presents)
            {
                pair.Key.EndRenderTarget(m_commandList);
            }

            m_commandList.Close();
        }

        public void Execute()
        {
            graphicsDevice.commandQueue.ExecuteCommandList(m_commandList);
            graphicsDevice.ReturnCommandList(m_commandList);
            m_commandList = null;

            foreach (var pair in presents)
            {
                pair.Key.Present(pair.Value);
            }
            presents.Clear();

            foreach (var resource in referenceThisCommand)
            {
                graphicsDevice.ResourceDelayRecycle(resource);
            }
            referenceThisCommand.Clear();
        }

        public void Present(SwapChain swapChain, bool vsync)
        {
            presents[swapChain] = vsync;
        }

        void CreateBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.CopyDestination, HeapType heapType = HeapType.Default)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(heapType),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)bufferLength),
                resourceStates,
                null,
                out resource));
        }
        void CreateUAVBuffer(int bufferLength, ref ID3D12Resource resource, ResourceStates resourceStates = ResourceStates.UnorderedAccess)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(HeapType.Default),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)bufferLength, ResourceFlags.AllowUnorderedAccess),
                resourceStates,
                null,
                out resource));
        }

        void CreateResource(ResourceDescription resourceDescription, ClearValue? clearValue, ref ID3D12Resource resource,
            ResourceStates resourceStates = ResourceStates.CopyDestination, HeapType heapType = HeapType.Default)
        {
            graphicsDevice.ResourceDelayRecycle(resource);
            ThrowIfFailed(graphicsDevice.device.CreateCommittedResource(
                new HeapProperties(heapType),
                HeapFlags.None,
                resourceDescription,
                resourceStates,
                clearValue,
                out resource));
        }

        void _RTWriteGpuAddr<T>(Span<T> data, BinaryWriter writer) where T : unmanaged
        {
            GetRingBuffer().Upload(data, out ulong addr);
            writer.Write(addr);
        }

        GpuDescriptorHandle GetUAVHandle(Texture2D texture, ResourceStates state= ResourceStates.UnorderedAccess)
        {
            texture.StateChange(m_commandList, state);

            return CreateUAV(texture.resource, null);
        }

        GpuDescriptorHandle GetUAVHandle(TextureCube texture)
        {
            //texture.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            texture.SetAllResourceState(m_commandList, ResourceStates.UnorderedAccess);
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = texture.uavFormat,
                ViewDimension = UnorderedAccessViewDimension.Texture2DArray,
            };
            uavDesc.Texture2DArray.ArraySize = 6;

            return CreateUAV(texture.resource, uavDesc);
        }
        GpuDescriptorHandle GetUAVHandle(GPUBuffer buffer)
        {
            buffer.StateChange(m_commandList, ResourceStates.UnorderedAccess);
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = Format.R32_Typeless,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView()
                {
                    Flags = BufferUnorderedAccessViewFlags.Raw,
                    NumElements = buffer.size / 4
                }
            };

            return CreateUAV(buffer.resource, uavDesc);
        }
        GpuDescriptorHandle GetSRVHandle(GPUBuffer buffer)
        {
            buffer.StateChange(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription()
            {
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                Format = Format.R32_Typeless,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Buffer
            };
            srvDesc.Buffer.FirstElement = 0;
            srvDesc.Buffer.NumElements = buffer.size / 4;
            srvDesc.Buffer.Flags = BufferShaderResourceViewFlags.Raw;

            return CreateSRV(buffer.resource, srvDesc);
        }
        GpuDescriptorHandle GetSRVHandle(Texture2D texture, bool linear = false)
        {
            texture.StateChange(m_commandList, ResourceStates.GenericRead);

            var format = texture.format;
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription
            {
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D,
                Format = (linear && format == Format.R8G8B8A8_UNorm_SRgb) ? Format.R8G8B8A8_UNorm : format
            };
            srvDesc.Texture2D.MipLevels = texture.mipLevels;

            return CreateSRV(texture.resource, srvDesc);
        }

        GpuDescriptorHandle GetSRVHandle(TextureCube texture)
        {
            texture.SetAllResourceState(m_commandList, ResourceStates.GenericRead);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            srvDesc.Format = texture.format;
            srvDesc.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube;
            srvDesc.TextureCube.MipLevels = texture.mipLevels;

            return CreateSRV(texture.resource, srvDesc);
        }

        GpuDescriptorHandle GetSRVHandleWithMip(TextureCube texture, int mips)
        {
            //texture.StateChange(m_commandList, ResourceStates.GenericRead);
            texture.SetPartResourceState(m_commandList, ResourceStates.GenericRead, 0, mips);
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription
            {
                Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                Format = texture.format,
                ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.TextureCube
            };
            srvDesc.TextureCube.MipLevels = mips;

            return CreateSRV(texture.resource, srvDesc);
        }

        GpuDescriptorHandle CreateSRV(ID3D12Resource resource, ShaderResourceViewDescription srvDesc)
        {
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
            graphicsDevice.device.CreateShaderResourceView(resource, srvDesc, cpuHandle);
            InReference(resource);
            return gpuHandle;
        }

        GpuDescriptorHandle CreateUAV(ID3D12Resource resource, UnorderedAccessViewDescription? uavDesc)
        {
            graphicsDevice.cbvsrvuavHeap.GetTempHandle(out var cpuHandle, out var gpuHandle);
            graphicsDevice.device.CreateUnorderedAccessView(resource, null, uavDesc, cpuHandle);
            InReference(resource);
            return gpuHandle;
        }

        void InReference(ID3D12Object iD3D12Object)
        {
            if (referenceThisCommand.Add(iD3D12Object))
                iD3D12Object.AddRef();
        }
        ulong InReferenceAddr(ID3D12Resource iD3D12Object)
        {
            if (referenceThisCommand.Add(iD3D12Object))
                iD3D12Object.AddRef();
            return iD3D12Object.GPUVirtualAddress;
        }
        public HashSet<ID3D12Object> referenceThisCommand = new HashSet<ID3D12Object>();

        public UnnamedInputLayout currentInputLayout;

        Dictionary<SwapChain, bool> presents = new Dictionary<SwapChain, bool>();

        RingBuffer GetRingBuffer() => graphicsDevice.superRingBuffer;
    }
}
