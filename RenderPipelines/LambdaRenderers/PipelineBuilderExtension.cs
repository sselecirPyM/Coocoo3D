using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DXGI;
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

                Span<byte> bufferData = stackalloc byte[64];
                foreach (var viewport in s.viewports)
                {
                    context.renderWrap.SetRenderTargetDepth(viewport.RenderTarget, false);
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
                            context.SetMesh(mesh);
                        }
                        MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform * viewport.viewProjection));
                        if (renderable.drawDoubleFace)
                            desc.cullMode = CullMode.None;
                        else
                            desc.cullMode = CullMode.Back;
                        context.SetPSO(p.shader_shadow, desc);
                        context.SetCBV(0, bufferData);
                        context.Draw(renderable);
                    }
                }
            });


            builder.AddRenderer<SkyboxRenderConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                context.renderWrap.SetRenderTarget(s.RenderTarget, false);
                context.SetPSO(p.shader_skybox, s.psoDesc);
                context.SetSRV(0, s.skybox);

                Span<float> vertexs = stackalloc float[16];
                Span<ushort> indices = [0, 1, 2, 2, 1, 3];

                for (int i = 0; i < 4; i++)
                {
                    Vector4 b = Vector4.Transform(quadArray[i], s.camera.pvMatrix);
                    b /= b.W;
                    Vector3 dir = new Vector3(b.X, b.Y, b.Z) - s.camera.Position;
                    dir.CopyTo(vertexs[(i * 4)..]);
                }
                context.SetCBV<float>(0, [s.SkyLightMultiple]);

                context.SetSimpleMesh(MemoryMarshal.AsBytes(vertexs), MemoryMarshal.AsBytes(indices), 16, 2);
                context.DrawIndexedInstanced(6, 1, 0, 0, 0);
            });

            builder.AddRenderer<DrawObjectConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                RenderWrap renderWrap = context.renderWrap;
                renderWrap.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), s.DepthStencil, false, false);

                var desc = s.GetPSODesc(s.psoDesc);

                var writer = context.Writer;
                writer.Clear();
                if (s.CBVPerPass != null)
                {
                    context.Write(s.CBVPerPass, writer);
                    writer.SetCBV(2);
                }

                s.keywords2.Clear();
                foreach (var srv in s.additionalSRV)
                {
                    if (srv.Value is byte[] data)
                    {
                        context.SetSRV(srv.Key, data);
                    }
                    else if (srv.Value is GPUBuffer buffer)
                    {
                        context.SetSRV(srv.Key, buffer);
                    }
                }
                Span<byte> bufferData = stackalloc byte[80];
                Mesh mesh = null;
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
                        context.SetMesh(mesh);
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
                    renderWrap.SetShader(s.shader, desc, s.keywords2);

                    MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                    //MemoryMarshal.Write(bufferData.Slice(64), material.Metallic);
                    //MemoryMarshal.Write(bufferData.Slice(64 + 4), material.Roughness);
                    //MemoryMarshal.Write(bufferData.Slice(64 + 8), material.Emissive);
                    //MemoryMarshal.Write(bufferData.Slice(64 + 12), material.Specular);
                    s.WriteCBuffer(bufferData.Slice(64), material);
                    context.SetCBV(1, bufferData);

                    //renderWrap.SetSRVs(srvs, renderable.material);
                    context.SetSRV(0, material._Albedo);
                    context.SetSRV(1, material._Metallic);
                    context.SetSRV(2, material._Roughness);
                    context.SetSRV(3, material._Emissive);
                    context.SetSRV(4, material._Normal);
                    context.SetSRV(5, material._Spa);
                    context.SetSRV(6, s._ShadowMap);
                    context.SetSRV(7, s._Environment);
                    context.SetSRV(8, s._BRDFLUT);

                    context.Draw(renderable);
                    s.keywords2.Clear();
                }
            });

            builder.AddRenderer<TAAConfig>((s, c) =>
            {
                s.cbv[0] = s.camera.vpMatrix;
                s.cbv[1] = s.camera.pvMatrix;
                s.cbv[2] = s.historyCamera.vpMatrix;
                s.cbv[3] = s.historyCamera.pvMatrix;
                s.cbv[4] = s.target.width;
                s.cbv[5] = s.target.height;
                s.cbv[6] = s.camera.far;
                s.cbv[7] = s.camera.near;
                s.cbv[8] = s.TAAFactor;
            }, (s, c) =>
            {
                if (!s.EnableTAA)
                    return;
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;

                Keyword_shader_TAA flags = new Keyword_shader_TAA();
                if (s.DebugRenderType == DebugRenderType.TAA)
                    flags |= Keyword_shader_TAA.DEBUG_TAA;

                var writer = context.Writer;
                context.Write(s.cbv, writer);
                writer.SetCBV(0);

                context.SetSRVs(s.depth, s.history, s.historyDepth);
                context.SetUAV(0, s.target);
                context.SetPSO(p.shader_TAA.Get(flags));
                context.Dispatch((s.target.width + 7) / 8, (s.target.height + 7) / 8);
            });

            builder.AddRenderer<DrawGBufferConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var renderWrap = context.renderWrap;
                renderWrap.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), s.DepthStencil, false, false);
                var desc = s.GetPSODesc(context, s.psoDesc);

                s.keywords2.Clear();
                Span<byte> bufferData = stackalloc byte[176];
                Mesh mesh = null;
                foreach (var renderable in context.Renderables)
                {
                    var material = renderable.material;
                    if (material.IsTransparent)
                        continue;
                    if (mesh != renderable.mesh)
                    {
                        mesh = renderable.mesh;
                        context.SetMesh(mesh);
                    }

                    if (material.UseNormalMap)
                        s.keywords2.Add(("USE_NORMAL_MAP", "1"));
                    if (material.UseSpa)
                        s.keywords2.Add(("USE_SPA", "1"));

                    if (renderable.drawDoubleFace)
                        desc.cullMode = CullMode.None;
                    else
                        desc.cullMode = CullMode.Back;
                    context.renderWrap.SetShader(s.shader, desc, s.keywords2);


                    MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                    MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(s.viewProjection));
                    MemoryMarshal.Write(bufferData.Slice(128), material.Metallic);
                    MemoryMarshal.Write(bufferData.Slice(128 + 4), material.Roughness);
                    MemoryMarshal.Write(bufferData.Slice(128 + 8), material.Emissive);
                    MemoryMarshal.Write(bufferData.Slice(128 + 12), material.Specular);
                    MemoryMarshal.Write(bufferData.Slice(144), material.AO);
                    MemoryMarshal.Write(bufferData.Slice(144 + 4), s.CameraLeft);
                    MemoryMarshal.Write(bufferData.Slice(160), s.CameraDown);
                    context.SetCBV(1, bufferData);

                    context.SetSRV(0, material._Albedo);
                    context.SetSRV(1, material._Metallic);
                    context.SetSRV(2, material._Roughness);
                    context.SetSRV(3, material._Emissive);
                    context.SetSRV(4, material._Normal);
                    context.SetSRV(5, material._Spa);

                    context.Draw(renderable);
                    s.keywords2.Clear();
                }
            });

            builder.AddRenderer<DrawDecalConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var renderWrap = context.renderWrap;
                renderWrap.SetRenderTarget(CollectionsMarshal.AsSpan(s.RenderTargets), null, false, false);


                BoundingFrustum frustum = new(s.ViewProjection);

                Span<byte> bufferData = stackalloc byte[64 + 64 + 16];

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


                    Matrix4x4 m = transform.GetMatrix() * s.ViewProjection;
                    Matrix4x4.Invert(m, out var im);
                    MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(m));
                    MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(im));
                    MemoryMarshal.Write(bufferData.Slice(128), decalMaterial._DecalEmissivePower);
                    context.SetCBV<byte>(0, bufferData);

                    context.SetSRV(0, s.depthStencil);
                    context.SetSRV(1, decalMaterial.DecalColorTexture);
                    context.SetSRV(2, decalMaterial.DecalEmissiveTexture);

                    context.DrawCube();
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
                var renderWrap = context.renderWrap;


                context.SetPSO(p.shader_hiz1);


                int x = s.input.width;
                int y = s.input.height;
                context.SetCBV<int>(0, [x, y]);
                context.SetSRV(0, s.input);
                context.SetUAV(0, s.output);

                context.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
                context.SetPSO(p.shader_hiz2);
                for (int i = 1; i < 9; i++)
                {
                    x = (x + 1) / 2;
                    y = (y + 1) / 2;

                    context.SetCBV<int>(0, [x, y]);
                    context.SetSRV(0, s.output, i - 1);
                    context.SetUAV(0, s.output, i);

                    context.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
                }
            });

            builder.AddRenderer<DeferredShadingConfig>((s, c) =>
            {

            }, (s, c) =>
            {
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var renderWrap = context.renderWrap;

                renderWrap.SetRenderTarget(s.RenderTarget, null, false, false);

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

                var desc = s.GetPSODesc(renderWrap, s.psoDesc);
                renderWrap.SetShader(s.shader, desc, keywords2);

                context.SetSRV(0, pipelineMaterial.gbuffer0);
                context.SetSRV(1, pipelineMaterial.gbuffer1);
                context.SetSRV(2, pipelineMaterial.gbuffer2);
                context.SetSRV(3, pipelineMaterial.gbuffer3);
                context.SetSRV(4, pipelineMaterial._Environment);
                context.SetSRV(5, pipelineMaterial.depth);
                context.SetSRV(6, pipelineMaterial._ShadowMap);
                context.SetSRV(7, pipelineMaterial._SkyBox);
                context.SetSRV(8, pipelineMaterial._BRDFLUT);
                context.SetSRV(9, pipelineMaterial._HiZBuffer);
                context.SetSRV(10, pipelineMaterial.GIBuffer);


                context.SetSRV<PointLightData>(11, CollectionsMarshal.AsSpan(s.pointLightDatas));

                var writer = context.Writer;
                if (s.cbvs != null)
                    for (int i = 0; i < s.cbvs.Length; i++)
                    {
                        object[] cbv1 = s.cbvs[i];
                        if (cbv1 == null)
                            continue;
                        context.Write(cbv1, writer);
                        writer.SetCBV(i);
                    }
                context.DrawQuad();
                writer.Clear();
                keywords2.Clear();
            });

            builder.AddRenderer<PostProcessingConfig>((s, c) =>
            {

            }, (s, c) =>
            {

                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var renderWrap = context.renderWrap;

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
                    bloomPass.Execute(context);
                }

                srgbConvert.inputColor = s.inputColor;//srgbConvert.srvs[0] = inputColor;
                srgbConvert.inputColor1 = s.intermedia2;//srgbConvert.srvs[1] = "intermedia2";

                renderWrap.SetRenderTarget(s.output, false);
                srgbConvert.context = context;
                srgbConvert.Execute();
            });

            builder.AddRenderer<RayTracingConfig>((s, t) =>
            {

            }, (s, c) =>
            {
                if (!s.RayTracing && !s.RayTracingGI)
                    return;
                var p = c.GetResourceProvider<TestResourceProvider>();
                var context = p.RenderHelper;
                var renderWrap = context.renderWrap;
                var pipelineMaterial = s.pipelineMaterial;

                var graphicsContext = renderWrap.graphicsContext;

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
                    Path.GetFullPath(rayTracingShader.hlslFile, renderWrap.BasePath));

                if (!graphicsContext.SetPSO(rtpso))
                    return;
                var writer = context.Writer;

                var tpas = new RTTopLevelAcclerationStruct();
                tpas.instances = new();
                Span<byte> bufferData = stackalloc byte[256];
                foreach (var renderable in context.Renderables)
                {
                    var material = renderable.material;

                    var btas = new RTBottomLevelAccelerationStruct();

                    btas.mesh = renderable.mesh;

                    btas.indexStart = renderable.indexStart;
                    btas.indexCount = renderable.indexCount;
                    btas.vertexStart = renderable.vertexStart;
                    btas.vertexCount = renderable.vertexCount;
                    var instance = new RTInstance() { blas = btas };
                    instance.transform = renderable.transform;
                    instance.hitGroupName = "rayHit";
                    instance.SRVs = new();
                    instance.CBVs = new();

                    instance.SRVs.Add(4, material._Albedo);
                    instance.SRVs.Add(5, material._Metallic);
                    instance.SRVs.Add(6, material._Roughness);
                    instance.SRVs.Add(7, material._Emissive);


                    MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
                    MemoryMarshal.Write(bufferData.Slice(64), material.Metallic);
                    MemoryMarshal.Write(bufferData.Slice(64 + 4), material.Roughness);
                    MemoryMarshal.Write(bufferData.Slice(64 + 8), material.Emissive);
                    MemoryMarshal.Write(bufferData.Slice(64 + 12), material.Specular);
                    instance.CBVs.Add(0, bufferData.ToArray());
                    tpas.instances.Add(instance);
                }

                int width = s.renderTarget.width;
                int height = s.renderTarget.height;

                context.Write(RayTracingConfig.cbv0, writer);
                var cbvData0 = writer.GetData();


                RayTracingCall call = new RayTracingCall();
                call.tpas = tpas;
                call.UAVs = new();
                call.SRVs = new();
                call.CBVs = new();
                call.missShaders = RayTracingConfig.missShaders;

                call.UAVs[0] = s.renderTarget;
                call.UAVs[1] = pipelineMaterial.GIBufferWrite;

                call.SRVs[1] = pipelineMaterial._Environment;
                call.SRVs[2] = pipelineMaterial._BRDFLUT;
                call.SRVs[3] = pipelineMaterial.depth;
                call.SRVs[4] = pipelineMaterial.gbuffer0;
                call.SRVs[5] = pipelineMaterial.gbuffer1;
                call.SRVs[6] = pipelineMaterial.gbuffer2;
                call.SRVs[7] = pipelineMaterial._ShadowMap;
                call.SRVs[8] = pipelineMaterial.GIBuffer;

                call.CBVs.Add(0, cbvData0);

                graphicsContext.BuildAccelerationStruct(tpas);
                if (s.RayTracingGI)
                {
                    call.rayGenShader = "rayGenGI";
                    graphicsContext.DispatchRays(16, 16, 16, call);
                    renderWrap.Swap("GIBuffer", "GIBufferWrite");
                }
                if (s.RayTracing)
                {
                    call.rayGenShader = "rayGen";
                    graphicsContext.DispatchRays(width, height, 1, call);
                }
            });
        }
    }
}
