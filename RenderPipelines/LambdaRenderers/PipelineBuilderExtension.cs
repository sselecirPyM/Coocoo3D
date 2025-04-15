using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;
using static RenderPipelines.LambdaRenderers.TestResourceProvider;

namespace RenderPipelines.LambdaRenderers
{
    public static class PipelineBuilderExtension
    {
        static Vector4[] quadArray = new Vector4[]
        {
            new Vector4(-1, -1, 0, 1),
            new Vector4(1, -1, 0, 1),
            new Vector4(-1, 1, 0, 1),
            new Vector4(1, 1, 0, 1),
        };
        public static void AddRenderers(this PipelineBuilder builder)
        {
            builder.AddRenderer<ShadowRenderConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var graphicsContext = context.renderPipelineView.graphicsContext;

                foreach (var viewport in s.viewports)
                {
                    context.renderPipelineView.SetRenderTargetDepth(viewport.RenderTarget, false);
                    var Rectangle = viewport.Rectangle;
                    context.SetScissorRectAndViewport(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Bottom);
                    var desc = s.psoDesc;
                    var dsv = viewport.RenderTarget;
                    desc.dsvFormat = dsv.GetFormat();


                    Mesh mesh = null;

                    foreach (var renderable in s.renderables)
                    {
                        if (mesh != renderable.mesh)
                        {
                            mesh = renderable.mesh;
                            graphicsContext.SetMesh(mesh);
                        }

                        if (renderable.drawDoubleFace)
                            desc.cullMode = CullMode.None;
                        else
                            desc.cullMode = CullMode.Back;
                        context.SetPSO(p.shader_shadow, desc);
                        graphicsContext.SetGraphicsResources((ct) =>
                        {
                            ct.SetCBV(0, [Matrix4x4.Transpose(renderable.transform * viewport.viewProjection)]);
                        });
                        graphicsContext.PipelineBinding2();
                        graphicsContext.Draw(renderable);
                    }
                }
            });


            builder.AddRenderer<SkyboxRenderConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                context.renderPipelineView.SetRenderTarget(s.RenderTarget, false);
                context.SetPSO(p.shader_skybox, s.psoDesc);

                Span<float> vertexs = stackalloc float[16];
                Span<ushort> indices = [0, 1, 2, 2, 1, 3];

                for (int i = 0; i < 4; i++)
                {
                    Vector4 b = Vector4.Transform(quadArray[i], s.camera.pvMatrix);
                    b /= b.W;
                    Vector3 dir = new Vector3(b.X, b.Y, b.Z) - s.camera.Position;
                    dir.CopyTo(vertexs[(i * 4)..]);
                }
                context.renderPipelineView.graphicsContext.SetGraphicsResources((ct) =>
                {
                    ct.SetSRV(0, s.skybox);
                    ct.SetCBV(0, [s.SkyLightMultiple]);
                });

                context.SetSimpleMesh(MemoryMarshal.AsBytes(vertexs), MemoryMarshal.AsBytes(indices), 16, 2);
                context.renderPipelineView.graphicsContext.DrawIndexedInstanced2(6, 1, 0, 0, 0);
            });

            builder.AddRenderer<DrawObjectConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var graphicsContext = view.graphicsContext;
                view.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), s.DepthStencil, false, false);

                var desc = s.GetPSODesc(s.psoDesc);

                var writer = context.Writer;
                writer.Clear();



                //ulong cbv2 = 0;
                //if (s.CBVPerPass != null)
                //{
                //    context.Write(s.CBVPerPass, writer);
                //    cbv2 = graphicsContext.UploadCBV<byte>(writer.GetData());
                //}

                int[] srvKeys = s.additionalSRV.Keys.ToArray();
                foreach (var srvKey in srvKeys)
                {
                    var o = s.additionalSRV[srvKey];

                    if (o is byte[] data)
                    {
                        var handle = graphicsContext.UploadSRV<byte>(data);
                        s.additionalSRV[srvKey] = handle;
                    }
                }

                s.keywords2.Clear();
                Mesh mesh = null;
                bool shaderChanged = true;
                foreach (var renderable in context.Renderables)
                {
                    var material = renderable.material;
                    if (material.IsTransparent && !s.DrawTransparent)
                    {
                        continue;
                    }
                    if (!material.IsTransparent && !s.DrawOpaque)
                    {
                        continue;
                    }

                    if (mesh != renderable.mesh)
                    {
                        mesh = renderable.mesh;
                        graphicsContext.SetMesh(mesh);
                    }
                    s.keywords2.AddRange(s.keywords);
                    if (material.UseNormalMap)
                        s.keywords2.Add(("USE_NORMAL_MAP", "1"));
                    if (material.UseSpa)
                        s.keywords2.Add(("USE_SPA", "1"));

                    if (renderable.drawDoubleFace)
                        desc.cullMode = CullMode.None;
                    else
                        desc.cullMode = CullMode.Back;
                    view.SetShader(s.shader, desc, s.keywords2);

                    graphicsContext.SetGraphicsResources((ct) =>
                    {
                        Span<byte> bufferData = stackalloc byte[80];
                        MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                        s.WriteCBuffer(bufferData.Slice(64), material);

                        foreach (var srv in s.additionalSRV)
                        {
                            if (srv.Value is Vortice.Direct3D12.GpuDescriptorHandle handle1)
                            {
                                ct.SetSRV(srv.Key, handle1);
                            }
                            else if (srv.Value is GPUBuffer buffer)
                            {
                                ct.SetSRV(srv.Key, buffer);
                            }
                        }

                        ct.SetCBV<byte>(1, bufferData);
                        //if (cbv2 != 0)
                        //{
                        //    ct.SetCBV(2, cbv2);
                        //}
                        ct.SetSRV(0, material._Albedo);
                        ct.SetSRV(1, material._Metallic);
                        ct.SetSRV(2, material._Roughness);
                        ct.SetSRV(3, material._Emissive);
                        ct.SetSRV(4, material._Normal);
                        ct.SetSRV(5, material._Spa);
                        ct.SetSRV(6, s._ShadowMap);
                        ct.SetSRV(7, s._Environment);
                        ct.SetSRV(8, s._BRDFLUT);
                        if (shaderChanged)
                            s.Binding?.Invoke(ct);
                        shaderChanged = false;
                    });
                    graphicsContext.PipelineBinding2();
                    graphicsContext.Draw(renderable);
                    s.keywords2.Clear();
                }
            });

            builder.AddRenderer<TAAConfig>((s, c) =>
            {
            }, (s, c) =>
            {
                if (!s.EnableTAA)
                    return;
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var graphicsContext = context.renderPipelineView.graphicsContext;

                Keyword_shader_TAA flags = new Keyword_shader_TAA();
                if (s.DebugRenderType == DebugRenderType.TAA)
                    flags |= Keyword_shader_TAA.DEBUG_TAA;

                graphicsContext.SetPSO(p.shader_TAA.Get(flags));
                graphicsContext.SetComputeResources((ct) =>
                {
                    ct.SetSRVs(s.depth, s.history, s.historyDepth);
                    ct.SetUAV(0, s.target);
                    ct.SetCBV(0, (p) =>
                    {
                        p.Set("g_mWorldToProj", [Matrix4x4.Transpose(s.camera.vpMatrix), Matrix4x4.Transpose(s.camera.pvMatrix)]);
                        p.Set("g_mWorldToProj1", [Matrix4x4.Transpose(s.historyCamera.vpMatrix), Matrix4x4.Transpose(s.historyCamera.pvMatrix)]);
                        p.Set("_widthHeight", [s.target.width, s.target.height]);
                        p.Set("g_cameraFarClip", s.camera.far);
                        p.Set("g_cameraNearClip", s.camera.near);
                        p.Set("mixFactor", s.TAAFactor);
                    });
                });
                graphicsContext.Dispatch((s.target.width + 7) / 8, (s.target.height + 7) / 8, 1);
            });

            builder.AddRenderer<DrawGBufferConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var graphicsContext = view.graphicsContext;
                view.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), s.DepthStencil, false, false);
                var desc = s.GetPSODesc(context, s.psoDesc);

                s.keywords2.Clear();
                Mesh mesh = null;
                foreach (var renderable in context.Renderables)
                {
                    var material = renderable.material;
                    if (material.IsTransparent)
                        continue;
                    if (mesh != renderable.mesh)
                    {
                        mesh = renderable.mesh;
                        graphicsContext.SetMesh(mesh);
                    }

                    if (material.UseNormalMap)
                        s.keywords2.Add(("USE_NORMAL_MAP", "1"));
                    if (material.UseSpa)
                        s.keywords2.Add(("USE_SPA", "1"));

                    if (renderable.drawDoubleFace)
                        desc.cullMode = CullMode.None;
                    else
                        desc.cullMode = CullMode.Back;
                    view.SetShader(s.shader, desc, s.keywords2);


                    graphicsContext.SetGraphicsResources((ct) =>
                    {
                        Span<byte> bufferData = stackalloc byte[176];
                        MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                        MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(s.viewProjection));
                        MemoryMarshal.Write(bufferData.Slice(128), material.Metallic);
                        MemoryMarshal.Write(bufferData.Slice(128 + 4), material.Roughness);
                        MemoryMarshal.Write(bufferData.Slice(128 + 8), material.Emissive);
                        MemoryMarshal.Write(bufferData.Slice(128 + 12), material.Specular);
                        MemoryMarshal.Write(bufferData.Slice(144), material.AO);
                        MemoryMarshal.Write(bufferData.Slice(144 + 4), s.CameraLeft);
                        MemoryMarshal.Write(bufferData.Slice(160), s.CameraDown);


                        ct.SetCBV<byte>(1, bufferData);
                        ct.SetSRV(0, material._Albedo);
                        ct.SetSRV(1, material._Metallic);
                        ct.SetSRV(2, material._Roughness);
                        ct.SetSRV(3, material._Emissive);
                        ct.SetSRV(4, material._Normal);
                        ct.SetSRV(5, material._Spa);
                    });
                    graphicsContext.PipelineBinding2();
                    graphicsContext.Draw(renderable);
                    s.keywords2.Clear();
                }
            });

            builder.AddRenderer<DrawDecalConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var graphicsContext = view.graphicsContext;
                view.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), null, false, false);


                BoundingFrustum frustum = new(s.ViewProjection);


                foreach (var visual in s.Visuals)
                {
                    if (visual.material.Type != Caprice.Display.UIShowType.Decal)
                        continue;

                    ref var transform = ref visual.transform;

                    if (!frustum.Intersects(new BoundingSphere(transform.position, transform.scale.Length())))
                        continue;

                    var decalMaterial = DictExt.ConvertToObject<DecalMaterial>(visual.material.Parameters, context);

                    DrawDecalFlag flag = DrawDecalFlag.None;

                    if (decalMaterial.EnableDecalColor)
                        flag |= DrawDecalFlag.ENABLE_DECAL_COLOR;
                    if (decalMaterial.EnableDecalEmissive)
                        flag |= DrawDecalFlag.ENABLE_DECAL_EMISSIVE;

                    context.SetPSO(p.shader_drawDecal.Get(flag), s.psoDesc);

                    graphicsContext.SetGraphicsResources((ct) =>
                    {
                        Span<byte> bufferData = stackalloc byte[64 + 64 + 16];
                        Matrix4x4 m = visual.transform.GetMatrix() * s.ViewProjection;
                        Matrix4x4.Invert(m, out var im);
                        MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(m));
                        MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(im));
                        MemoryMarshal.Write(bufferData.Slice(128), decalMaterial._DecalEmissivePower);
                        ct.SetCBV<byte>(0, bufferData);

                        ct.SetSRV(0, s.depthStencil);
                        ct.SetSRV(1, decalMaterial.DecalColorTexture);
                        ct.SetSRV(2, decalMaterial.DecalEmissiveTexture);
                    });
                    graphicsContext.PipelineBinding2();
                    context.DrawCube2();
                }
            });

            builder.AddRenderer<HizConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                if (!s.Enable)
                    return;
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var graphicsContext = view.graphicsContext;


                graphicsContext.SetPSO(p.shader_hiz1);
                int x = s.input.width;
                int y = s.input.height;
                graphicsContext.SetComputeResources(ct =>
                {
                    ct.SetCBV(0, [x, y]);
                    ct.SetSRV(0, s.input);
                    ct.SetUAV(0, s.output);
                });
                graphicsContext.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
                graphicsContext.SetPSO(p.shader_hiz2);
                for (int i = 1; i < 9; i++)
                {
                    x = (x + 1) / 2;
                    y = (y + 1) / 2;

                    graphicsContext.SetComputeResources(ct =>
                    {
                        ct.SetCBV<int>(0, [x, y]);
                        ct.SetSRVMip(0, s.output, i - 1);
                        ct.SetUAVMip(0, s.output, i);
                    });

                    graphicsContext.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
                }
            });

            builder.AddRenderer<DeferredShadingConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var graphicsContext = view.graphicsContext;

                view.SetRenderTarget(s.RenderTarget, null, false, false);

                var pipelineMaterial = s.pipelineMaterial;
                var keywords2 = s.keywords2;
                keywords2.Clear();
                keywords2.AddRange(s.keywords);

                if (s.EnableFog)
                    keywords2.Add(("ENABLE_FOG", "1"));
                if (s.EnableSSAO)
                    keywords2.Add(("ENABLE_SSAO", "1"));
                if (s.EnableSSR)
                    keywords2.Add(("ENABLE_SSR", "1"));
                if (s.UseGI)
                    keywords2.Add(("ENABLE_GI", "1"));
                if (s.NoBackGround)
                    keywords2.Add(("DISABLE_BACKGROUND", "1"));

                var desc = s.GetPSODesc(view, s.psoDesc);
                view.SetShader(s.shader, desc, keywords2);

                //context.SetSRV(0, pipelineMaterial.gbuffer0);
                //context.SetSRV(1, pipelineMaterial.gbuffer1);
                //context.SetSRV(2, pipelineMaterial.gbuffer2);
                //context.SetSRV(3, pipelineMaterial.gbuffer3);
                //context.SetSRV(4, pipelineMaterial._Environment);
                //context.SetSRV(5, pipelineMaterial.depth);
                //context.SetSRV(6, pipelineMaterial._ShadowMap);
                //context.SetSRV(7, pipelineMaterial._SkyBox);
                //context.SetSRV(8, pipelineMaterial._BRDFLUT);
                //context.SetSRV(9, pipelineMaterial._HiZBuffer);
                //context.SetSRV(10, pipelineMaterial.GIBuffer);


                //context.SetSRV<PointLightData>(11, CollectionsMarshal.AsSpan(s.pointLightDatas));

                //var writer = context.Writer;
                //if (s.cbvs != null)
                //    for (int i = 0; i < s.cbvs.Length; i++)
                //    {
                //        object[] cbv1 = s.cbvs[i];
                //        if (cbv1 == null)
                //            continue;
                //        context.Write(cbv1, writer);
                //        writer.SetCBV(i);
                //    }
                //context.DrawQuad();

                graphicsContext.SetGraphicsResources((ct) =>
                {
                    ct.SetSRV(0, pipelineMaterial.gbuffer0);
                    ct.SetSRV(1, pipelineMaterial.gbuffer1);
                    ct.SetSRV(2, pipelineMaterial.gbuffer2);
                    ct.SetSRV(3, pipelineMaterial.gbuffer3);
                    ct.SetSRV(4, pipelineMaterial._Environment);
                    ct.SetSRV(5, pipelineMaterial.depth);
                    ct.SetSRV(6, pipelineMaterial._ShadowMap);
                    ct.SetSRV(7, pipelineMaterial._SkyBox);
                    ct.SetSRV(8, pipelineMaterial._BRDFLUT);
                    ct.SetSRV(9, pipelineMaterial._HiZBuffer);
                    ct.SetSRV(10, pipelineMaterial.GIBuffer);


                    ct.SetSRV<PointLightData>(11, CollectionsMarshal.AsSpan(s.pointLightDatas));

                    //var writer = context.Writer;
                    //if (s.cbvs != null)
                    //    for (int i = 0; i < s.cbvs.Length; i++)
                    //    {
                    //        object[] cbv1 = s.cbvs[i];
                    //        if (cbv1 == null)
                    //            continue;
                    //        context.Write(cbv1, writer);
                    //        ct.SetCBV(i, writer.GetData());
                    //    }
                    s.Binding?.Invoke(ct);
                    //writer.Clear();
                });
                graphicsContext.PipelineBinding2();
                context.DrawQuad2();
                keywords2.Clear();
            });

            builder.AddRenderer<PostProcessingConfig>((s, c) =>
            {

            }, (s, c) =>
            {

                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;

                var generateMipPass = p.generateMipPass;
                var bloomPass = p.bloomPass;
                var srgbConvert = p.srgbConvert;

                if (s.EnableBloom)
                {
                    generateMipPass.input = s.inputColor;
                    generateMipPass.output = s.intermedia3;
                    generateMipPass.context = context;
                    generateMipPass.Execute();

                    int r = 0;
                    uint n = (uint)(s.inputColor.height / 1024);
                    while (n > 0)
                    {
                        r++;
                        n >>= 1;
                    }
                    bloomPass.intermediaTexture = s.intermedia1;
                    bloomPass.mipLevel = r;
                    bloomPass.inputSize = (s.inputColor.width / 2, s.inputColor.height / 2);

                    bloomPass.input = s.intermedia3;
                    bloomPass.output = s.intermedia2;
                    bloomPass.BloomThreshold = s.BloomThreshold;
                    bloomPass.BloomIntensity = s.BloomIntensity;
                    bloomPass.Execute(view);
                }

                srgbConvert.inputColor = s.inputColor;//srgbConvert.srvs[0] = inputColor;
                srgbConvert.inputColor1 = s.intermedia2;//srgbConvert.srvs[1] = "intermedia2";

                view.SetRenderTarget(s.output, false);
                srgbConvert.context = context;
                srgbConvert.Execute();
            });

            builder.AddRenderer<RayTracingConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                if (!s.RayTracing && !s.RayTracingGI)
                    return;
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var view = context.renderPipelineView;
                var pipelineMaterial = s.pipelineMaterial;

                var graphicsContext = view.graphicsContext;

                var keywords1 = s.keywords1;

                var rayTracingShader = p.GetRayTracingShader();

                keywords1.Clear();
                if (s.directionalLight != null)
                {
                    keywords1.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
                }
                if (s.UseGI)
                {
                    keywords1.Add(new("ENABLE_GI", "1"));
                }
                var rtpso = context.GetRTPSO(keywords1, rayTracingShader,
                    Path.GetFullPath(rayTracingShader.hlslFile, view.BasePath));

                if (!graphicsContext.SetPSO(rtpso))
                    return;
                var writer = context.Writer;

                var tlas = new RTTopLevelAcclerationStruct();
                tlas.instances = new();
                string[] nameIndex = new string[] { "POSITION0", "NORMAL0", "TEXCOORD0" };
                foreach (var renderable in context.Renderables)
                {
                    var material = renderable.material;

                    var blas = new RTBottomLevelAccelerationStruct();

                    blas.mesh = renderable.mesh;

                    blas.indexStart = renderable.indexStart;
                    blas.indexCount = renderable.indexCount;
                    blas.vertexStart = renderable.vertexStart;
                    blas.vertexCount = renderable.vertexCount;


                    var instance = new RTInstance() { blas = blas };
                    instance.transform = renderable.transform;

                    instance.SetLocalResource = (ct) =>
                    {
                        Span<byte> bufferData = stackalloc byte[256];
                        MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                        MemoryMarshal.Write(bufferData.Slice(64), material.Metallic);
                        MemoryMarshal.Write(bufferData.Slice(64 + 4), material.Roughness);
                        MemoryMarshal.Write(bufferData.Slice(64 + 8), material.Emissive);
                        MemoryMarshal.Write(bufferData.Slice(64 + 12), material.Specular);
                        ct.SetShader("rayHit");
                        ct.SetSRV(0, renderable.mesh.GetIndexBuffer().GPUVirtualAddress + (ulong)renderable.indexStart * 4);
                        for (int i = 0; i < nameIndex.Length; i++)
                        {
                            string name = nameIndex[i];
                            var vertexBufferView = renderable.mesh.GetVertexBufferView(name);
                            ulong address = vertexBufferView.BufferLocation + (ulong)(vertexBufferView.StrideInBytes * renderable.vertexStart);
                            ct.SetSRV(i + 1, address);
                        }
                        ct.SetSRV(4, material._Albedo);
                        ct.SetSRV(5, material._Metallic);
                        ct.SetSRV(6, material._Roughness);
                        ct.SetSRV(7, material._Emissive);
                        ct.SetCBV<byte>(0, bufferData);
                    };


                    tlas.instances.Add(instance);
                }

                int width = s.renderTarget.width;
                int height = s.renderTarget.height;

                context.Write(RayTracingConfig.cbv0, writer);
                var cbvData0 = writer.GetData();


                RayTracingCall call = new RayTracingCall();
                call.tlas = tlas;
                call.missShaders = RayTracingConfig.missShaders;

                call.SetResources = (ct) =>
                {
                    ct.SetCBV(0, cbvData0);
                    ct.SetSRV(0, tlas);
                    ct.SetSRV(1, pipelineMaterial._Environment, true);
                    ct.SetSRV(2, pipelineMaterial._BRDFLUT, true);
                    ct.SetSRV(3, pipelineMaterial.depth, true);
                    ct.SetSRV(4, pipelineMaterial.gbuffer0, true);
                    ct.SetSRV(5, pipelineMaterial.gbuffer1, true);
                    ct.SetSRV(6, pipelineMaterial.gbuffer2, true);
                    ct.SetSRV(7, pipelineMaterial._ShadowMap, true);
                    ct.SetSRV(8, pipelineMaterial.GIBuffer);
                    ct.SetUAV(0, s.renderTarget);
                    ct.SetUAV(1, pipelineMaterial.GIBufferWrite);
                };

                graphicsContext.BuildAccelerationStruct(tlas);
                if (s.RayTracingGI)
                {
                    call.rayGenShader = "rayGenGI";
                    graphicsContext.DispatchRays(16, 16, 16, call);
                    s.afterGI?.Invoke();
                }
                if (s.RayTracing)
                {
                    call.rayGenShader = "rayGen";
                    graphicsContext.DispatchRays(width, height, 1, call);
                }
            });
        }
        static void Draw<T>(this GraphicsContext graphicsContext, MeshRenderable<T> renderable)
        {
            graphicsContext.DrawIndexedInstanced2(renderable.indexCount, 1, renderable.indexStart, renderable.vertexStart, 0);
        }
    }
}
