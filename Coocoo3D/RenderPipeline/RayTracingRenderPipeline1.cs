﻿using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;

namespace Coocoo3D.RenderPipeline
{
    public class RayTracingRenderPipeline1 : RenderPipeline
    {
        public const int c_tempDataSize = 512;
        public const int c_entityDataDataSize = 128;
        const int c_materialDataSize = 512;
        const int c_presentDataSize = 512;
        const int c_lightCameraDataSize = 256;

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        static readonly RayTracingSceneSettings c_rayTracingSceneSettings = new RayTracingSceneSettings()
        {
            payloadSize = 32,
            attributeSize = 8,
            maxRecursionDepth = 5,
            rayTypeCount = 2,
        };

        RayTracingScene RayTracingScene = new RayTracingScene();
        Random randomGenerator = new Random();

        public SBuffer CameraDataBuffer = new SBuffer();
        public CBuffer LightCameraDataBuffer = new CBuffer();
        SBufferGroup materialBuffers1 = new SBufferGroup();

        public RayTracingRenderPipeline1()
        {
            materialBuffers1.Reload(c_materialDataSize, 65536);
        }

        public void Reload(DeviceResources deviceResources)
        {
            deviceResources.InitializeSBuffer(CameraDataBuffer, c_presentDataSize);
            deviceResources.InitializeCBuffer(LightCameraDataBuffer, c_lightCameraDataSize);
        }

        #region graphics assets
        static readonly string[] c_rayGenShaderNames = { "MyRaygenShader", "MyRaygenShader1" };
        static readonly string[] c_missShaderNames = { "MissShaderSurface", "MissShaderTest", };
        static readonly string[] c_hitGroupNames = new string[] { "HitGroupSurface", "HitGroupTest", };
        static readonly HitGroupDesc[] hitGroupDescs = new HitGroupDesc[]
        {
            new HitGroupDesc { HitGroupName = "HitGroupSurface", AnyHitName = "AnyHitShaderSurface", ClosestHitName = "ClosestHitShaderSurface" },
            new HitGroupDesc { HitGroupName = "HitGroupTest", AnyHitName = "AnyHitShaderTest", ClosestHitName = "ClosestHitShaderTest" },
        };
        static readonly string[] c_exportNames = new string[] { "MyRaygenShader", "MyRaygenShader1", "ClosestHitShaderSurface", "ClosestHitShaderTest", "MissShaderSurface", "MissShaderTest", "AnyHitShaderSurface", "AnyHitShaderTest", };

        public async Task ReloadAssets(DeviceResources deviceResources)
        {
            RayTracingScene.ReloadLibrary(await ReadFile("ms-appx:///Coocoo3DGraphics/Raytracing.cso"));
            RayTracingScene.ReloadPipelineStates(deviceResources, c_exportNames, hitGroupDescs, c_rayTracingSceneSettings);
            RayTracingScene.ReloadAllocScratchAndInstance(deviceResources, 1024 * 1024 * 64, 1024);
            Ready = true;
        }
        #endregion


        bool HasMainLight;
        int renderMatCount = 0;
        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var rendererComponents = context.dynamicContextRead.rendererComponents;
            var deviceResources = context.deviceResources;
            int countMaterials = 0;
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                countMaterials += rendererComponents[i].Materials.Count;
            }
            DesireMaterialBuffers(deviceResources, countMaterials);
            var cameras = context.dynamicContextRead.cameras;
            var camera = context.dynamicContextRead.cameras[0];
            ref var settings = ref context.dynamicContextRead.settings;
            ref var inShaderSettings = ref context.dynamicContextRead.inShaderSettings;
            var lightings = context.dynamicContextRead.lightings;

            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0);
            Matrix4x4 lightCameraMatrix = Matrix4x4.Identity;
            HasMainLight = false;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(256, camera.LookAtPoint, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix, pBufferData, true);
                graphicsContext.UpdateResource(LightCameraDataBuffer, context.bigBuffer, c_lightCameraDataSize, 0);
                HasMainLight = true;
            }

            PresentData cameraPresentData = new PresentData();
            cameraPresentData.PlayTime = (float)context.dynamicContextRead.Time;
            cameraPresentData.DeltaTime = (float)context.dynamicContextRead.DeltaTime;

            cameraPresentData.UpdateCameraData(cameras[0]);
            cameraPresentData.RandomValue1 = randomGenerator.Next(int.MinValue, int.MaxValue);
            cameraPresentData.RandomValue2 = randomGenerator.Next(int.MinValue, int.MaxValue);
            cameraPresentData.inShaderSettings = inShaderSettings;
            Marshal.StructureToPtr(cameraPresentData, pBufferData, true);
            Marshal.StructureToPtr(lightCameraMatrix, pBufferData + 256, true);
            graphicsContext.UpdateResource(CameraDataBuffer, context.bigBuffer, c_presentDataSize, 0);


            #region Update material data

            void WriteLightData(IList<LightingData> lightings1, IntPtr pBufferData1)
            {
                int lightCount1 = 0;
                for (int j = 0; j < lightings1.Count; j++)
                {
                    Marshal.StructureToPtr(lightings1[j].GetPositionOrDirection(), pBufferData1, true);
                    Marshal.StructureToPtr((uint)lightings1[j].LightingType, pBufferData1 + 12, true);
                    Marshal.StructureToPtr(lightings1[j].Color, pBufferData1 + 16, true);
                    lightCount1++;
                    pBufferData1 += 32;
                    if (lightCount1 >= 8)
                        break;
                }
            }
            _Counters counterMaterial = new _Counters();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var Materials = rendererComponents[i].Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    Array.Clear(context.bigBuffer, 0, c_materialDataSize);
                    Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    Marshal.StructureToPtr(counterMaterial.vertex, pBufferData + 240, true);
                    WriteLightData(lightings, pBufferData + RuntimeMaterial.c_materialDataSize);
                    materialBuffers1.UpdateSlience(graphicsContext, context.bigBuffer, 0, c_materialDataSize, counterMaterial.material);
                    counterMaterial.material++;
                }
                counterMaterial.vertex += rendererComponents[i].meshVertexCount;
            }
            #endregion
            renderMatCount = counterMaterial.material;
            if (renderMatCount > 0)
                materialBuffers1.UpdateSlienceComplete(graphicsContext);
        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var RPAssetsManager = context.RPAssetsManager;

            RayTracingScene.NextASIndex(renderMatCount);
            RayTracingScene.NextSTIndex();


            var rendererComponents = context.dynamicContextRead.rendererComponents;
            graphicsContext.SetRootSignature(RPAssetsManager.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);


            void EntitySkinning(MMDRendererComponent rendererComponent, SBuffer cameraPresentData, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                //graphicsContext.SetCBVR(entityDataBuffer, 1);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                var POSkinning = PObjectStatusSelect(context.deviceResources, RPAssetsManager.rootSignatureSkinning, ref context.SkinningDesc, rendererComponent.POSkinning, RPAssetsManager.PObjectMMDSkinning, RPAssetsManager.PObjectMMDSkinning, RPAssetsManager.PObjectMMDSkinning);
                int variant3 = POSkinning.GetVariantIndex(context.deviceResources, RPAssetsManager.rootSignatureSkinning, context.SkinningDesc);
                graphicsContext.SetPObject1(POSkinning, variant3);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < rendererComponents.Count; i++)
                EntitySkinning(rendererComponents[i], CameraDataBuffer, context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();

            graphicsContext.SetRootSignatureCompute(RPAssetsManager.rootSignatureCompute);

            void ParticleCompute(MMDRendererComponent rendererComponent, SBuffer cameraPresentData, CBuffer entityBoneDataBuffer, CBuffer entityDataBuffer, ref _Counters counter)
            {
                if (rendererComponent.ParticleCompute == null || rendererComponent.meshParticleBuffer == null || rendererComponent.ParticleCompute.Status != GraphicsObjectStatus.loaded)
                {
                    counter.vertex += rendererComponent.meshIndexCount;
                    return;
                }
                graphicsContext.SetComputeCBVR(entityBoneDataBuffer, 0);
                //graphicsContext.SetComputeCBVR(entityDataBuffer, 1);
                graphicsContext.SetComputeCBVR(cameraPresentData, 2);
                graphicsContext.SetComputeUAVR(context.SkinningMeshBuffer, counter.vertex, 4);
                graphicsContext.SetComputeUAVR(rendererComponent.meshParticleBuffer, 0, 5);
                graphicsContext.SetPObject(rendererComponent.ParticleCompute);
                graphicsContext.Dispatch((rendererComponent.meshVertexCount + 63) / 64, 1, 1);
                counter.vertex += rendererComponent.meshIndexCount;
            }
            _Counters counterParticle = new _Counters();
            for (int i = 0; i < rendererComponents.Count; i++)
                ParticleCompute(rendererComponents[i], CameraDataBuffer, context.CBs_Bone[i], null, ref counterParticle);

            if (HasMainLight && context.dynamicContextRead.inShaderSettings.EnableShadow)
            {
                graphicsContext.SetRootSignature(RPAssetsManager.rootSignature);
                graphicsContext.SetDSV(context.ShadowMapCube, 0, true);
                graphicsContext.SetMesh(context.SkinningMeshBuffer);

                void RenderEntityShadow(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, ref _Counters counter)
                {
                    Texture2D texLoading = context.TextureLoading;
                    Texture2D texError = context.TextureError;
                    var Materials = rendererComponent.Materials;
                    //graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                    //graphicsContext.SetCBVR(entityDataBuffer, 1);
                    graphicsContext.SetCBVR(cameraPresentData, 2);

                    graphicsContext.SetMeshIndex(rendererComponent.mesh);
                    SetPipelineStateVariant(context.deviceResources, graphicsContext, RPAssetsManager.rootSignature, ref context.shadowDesc, RPAssetsManager.PObjectMMDShadowDepth);
                    //List<Texture2D> texs = rendererComponent.textures;
                    //int countIndexLocal = 0;
                    //for (int i = 0; i < Materials.Count; i++)
                    //{
                    //    if (Materials[i].DrawFlags.HasFlag(DrawFlag.CastSelfShadow))
                    //    {
                    //        Texture2D tex1 = null;
                    //        if (Materials[i].texIndex != -1)
                    //            tex1 = texs[Materials[i].texIndex];
                    //        graphicsContext.SetCBVR(materialBuffers[counter.material], 3);
                    //        graphicsContext.SetSRVT(TextureStatusSelect(tex1, textureLoading, textureError, textureError), 4);
                    //        graphicsContext.DrawIndexed(Materials[i].indexCount, countIndexLocal, counter.vertex);
                    //    }
                    //    counter.material++;
                    //    countIndexLocal += Materials[i].indexCount;
                    //}
                    graphicsContext.DrawIndexed(rendererComponent.meshIndexCount, 0, counter.vertex);

                    counter.vertex += rendererComponent.meshVertexCount;
                }
                _Counters counterShadow = new _Counters();
                for (int i = 0; i < rendererComponents.Count; i++)
                    RenderEntityShadow(rendererComponents[i], LightCameraDataBuffer, ref counterShadow);
            }


            if (rendererComponents.Count > 0)
            {
                void BuildEntityBAS1(MMDRendererComponent rendererComponent, ref _Counters counter)
                {
                    Texture2D texLoading = context.TextureLoading;
                    Texture2D texError = context.TextureError;

                    var Materials = rendererComponent.Materials;
                    List<Texture2D> texs = rendererComponent.textures;

                    int numIndex = 0;
                    for (int i = 0; i < Materials.Count; i++)
                    {
                        Texture2D tex1 = null;
                        if (Materials[i].texIndex != -1)
                            tex1 = texs[Materials[i].texIndex];
                        tex1 = TextureStatusSelect(tex1, texLoading, texError, texError);

                        graphicsContext.BuildBASAndParam(RayTracingScene, context.SkinningMeshBuffer, rendererComponent.mesh, 0x1, counter.vertex, numIndex, Materials[i].indexCount, tex1,
                            materialBuffers1.constantBuffers[counter.material / materialBuffers1.sliencesPerBuffer], (counter.material % materialBuffers1.sliencesPerBuffer) * 2);
                        counter.material++;
                        numIndex += Materials[i].indexCount;
                    }
                    counter.vertex += rendererComponent.meshVertexCount;
                }
                _Counters counter1 = new _Counters();
                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    BuildEntityBAS1(rendererComponents[i], ref counter1);
                }
                graphicsContext.BuildTopAccelerationStructures(RayTracingScene);
                RayTracingScene.BuildShaderTable(context.deviceResources, c_rayGenShaderNames, c_missShaderNames, c_hitGroupNames, counter1.material);
                graphicsContext.SetRootSignatureRayTracing(RayTracingScene);
                graphicsContext.SetComputeUAVT(context.outputRTV, 0);
                graphicsContext.SetComputeCBVR(CameraDataBuffer, 2);
                graphicsContext.SetComputeSRVT(context.SkyBox, 3);
                graphicsContext.SetComputeSRVT(context.IrradianceMap, 4);
                graphicsContext.SetComputeSRVT(context.BRDFLut, 5);
                graphicsContext.SetComputeSRVTFace(context.ShadowMapCube, 0, 6);
                graphicsContext.SetComputeSRVR(context.SkinningMeshBuffer, 0, 7);
                graphicsContext.SetComputeUAVR(context.LightCacheBuffer, context.dynamicContextRead.frameRenderIndex % 2, 8);
                graphicsContext.SetComputeSRVR(context.LightCacheBuffer, (context.dynamicContextRead.frameRenderIndex + 1) % 2, 9);

                graphicsContext.DoRayTracing(RayTracingScene, context.dynamicContextRead.VertexCount, 1, 1);

                graphicsContext.SetComputeUAVR(context.LightCacheBuffer, (context.dynamicContextRead.frameRenderIndex + 1) % 2, 8);
                graphicsContext.SetComputeSRVR(context.LightCacheBuffer, context.dynamicContextRead.frameRenderIndex % 2, 9);
                graphicsContext.DoRayTracing(RayTracingScene, context.screenWidth, context.screenHeight, 0);
            }
            else
            {
                #region Render Sky box
                graphicsContext.SetRootSignature(RPAssetsManager.rootSignature);
                graphicsContext.SetRTVDSV(context.outputRTV, context.ScreenSizeDSVs[0], Vector4.Zero, true, true);
                graphicsContext.SetCBVR(CameraDataBuffer, 2);
                graphicsContext.SetSRVT(context.SkyBox, 6);
                graphicsContext.SetSRVT(context.IrradianceMap, 7);
                graphicsContext.SetSRVT(context.BRDFLut, 8);
                graphicsContext.SetMesh(context.ndcQuadMesh);
                PSODesc descSkyBox;
                descSkyBox.blendState = EBlendState.none;
                descSkyBox.cullMode = ECullMode.back;
                descSkyBox.depthBias = 0;
                descSkyBox.dsvFormat = context.depthFormat;
                descSkyBox.inputLayout = EInputLayout.postProcess;
                descSkyBox.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                descSkyBox.rtvFormat = context.outputFormat;
                descSkyBox.renderTargetCount = 1;
                descSkyBox.streamOutput = false;
                descSkyBox.wireFrame = false;
                SetPipelineStateVariant(context.deviceResources, graphicsContext, RPAssetsManager.rootSignature, ref descSkyBox, RPAssetsManager.PObjectSkyBox);

                graphicsContext.DrawIndexed(context.ndcQuadMesh.m_indexCount, 0, 0);
                #endregion
            }
        }

        private void DesireMaterialBuffers(DeviceResources deviceResources, int count)
        {
            materialBuffers1.SetSlienceCount(deviceResources, count);
        }
    }
}
