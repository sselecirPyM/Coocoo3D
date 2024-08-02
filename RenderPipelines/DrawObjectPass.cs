using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class DrawObjectPass
{
    public string shader;

    public List<(string, string)> keywords = new();
    List<(string, string)> keywords2 = new();

    public PSODesc psoDesc;

    public object[] CBVPerPass;

    public Dictionary<int, object> additionalSRV = new Dictionary<int, object>();

    public bool DrawOpaque;
    public bool DrawTransparent;
    public bool UseGI;
    public bool EnableFog;

    public Texture2D _ShadowMap;
    public Texture2D _Environment;
    public Texture2D _BRDFLUT;
    public GPUBuffer GIBuffer;

    public PSODesc GetPSODesc(RenderWrap renderWrap, PSODesc desc)
    {
        var rtvs = renderWrap.RenderTargets;
        var dsv = renderWrap.depthStencil;
        desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
        desc.renderTargetCount = rtvs.Count;

        return desc;
    }

    public void Execute(RenderHelper context)
    {
        RenderWrap renderWrap = context.renderWrap;

        var desc = GetPSODesc(context.renderWrap, psoDesc);

        var writer = context.Writer;
        writer.Clear();
        if (CBVPerPass != null)
        {
            context.Write(CBVPerPass, writer);
            writer.SetCBV(2);
        }

        keywords2.Clear();
        foreach (var srv in additionalSRV)
        {
            if (srv.Value is byte[] data)
            {
                context.SetSRV(srv.Key, data);
            }
        }
        Span<byte> bufferData = stackalloc byte[80];
        Mesh mesh = null;
        foreach (var renderable in context.Renderables)
        {
            var material = renderable.material;
            if (material.IsTransparent && !DrawTransparent)
            {
                continue;
            }
            if (!material.IsTransparent && !DrawOpaque)
            {
                continue;
            }

            if (mesh != renderable.mesh)
            {
                mesh = renderable.mesh;
                context.SetMesh(mesh);
            }
            keywords2.AddRange(this.keywords);
            if (material.UseNormalMap)
                keywords2.Add(("USE_NORMAL_MAP", "1"));
            if (material.UseSpa)
                keywords2.Add(("USE_SPA", "1"));
            if (UseGI)
                keywords2.Add(("ENABLE_GI", "1"));
            if (EnableFog)
                keywords2.Add(("ENABLE_FOG", "1"));

            if (renderable.drawDoubleFace)
                desc.cullMode = CullMode.None;
            else
                desc.cullMode = CullMode.Back;
            renderWrap.SetShader(shader, desc, keywords2);

            MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
            //MemoryMarshal.Write(bufferData.Slice(64), material.Metallic);
            //MemoryMarshal.Write(bufferData.Slice(64 + 4), material.Roughness);
            //MemoryMarshal.Write(bufferData.Slice(64 + 8), material.Emissive);
            //MemoryMarshal.Write(bufferData.Slice(64 + 12), material.Specular);
            WriteCBuffer(bufferData.Slice(64), material);
            context.SetCBV(1, bufferData);

            //renderWrap.SetSRVs(srvs, renderable.material);
            context.SetSRV(0, material._Albedo);
            context.SetSRV(1, material._Metallic);
            context.SetSRV(2, material._Roughness);
            context.SetSRV(3, material._Emissive);
            context.SetSRV(4, material._Normal);
            context.SetSRV(5, material._Spa);
            context.SetSRV(6, _ShadowMap);
            context.SetSRV(7, _Environment);
            context.SetSRV(8, _BRDFLUT);
            context.SetSRV(9, GIBuffer);

            context.Draw(renderable);
            keywords2.Clear();
        }
    }

    void WriteCBuffer(Span<byte> bufferData, ModelMaterial material)
    {
        MemoryMarshal.Write(bufferData.Slice(0), material.Metallic);
        MemoryMarshal.Write(bufferData.Slice(4), material.Roughness);
        MemoryMarshal.Write(bufferData.Slice(8), material.Emissive);
        MemoryMarshal.Write(bufferData.Slice(12), material.Specular);
    }
}