using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
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
        }
    }
}
