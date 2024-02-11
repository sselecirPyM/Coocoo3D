using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class DrawGBuffer
{
    string shader = "DeferredGBuffer.hlsl";

    List<(string, string)> keywords2 = new();

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
    };

    public Matrix4x4 viewProjection;

    public Vector3 CameraLeft;
    public Vector3 CameraDown;

    public PSODesc GetPSODesc(RenderHelper renderHelper, PSODesc desc)
    {
        var rtvs = renderHelper.renderWrap.RenderTargets;
        var dsv = renderHelper.renderWrap.depthStencil;
        desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
        desc.renderTargetCount = rtvs.Count;

        return desc;
    }

    public void Execute(RenderHelper context)
    {
        var desc = GetPSODesc(context, psoDesc);

        keywords2.Clear();
        Span<byte> bufferData = stackalloc byte[176];
        Mesh mesh = null;
        foreach (var renderable in context.MeshRenderables<ModelMaterial>())
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
                keywords2.Add(("USE_NORMAL_MAP", "1"));
            if (material.UseSpa)
                keywords2.Add(("USE_SPA", "1"));

            if (renderable.drawDoubleFace)
                desc.cullMode = CullMode.None;
            else
                desc.cullMode = CullMode.Back;
            context.renderWrap.SetShader(shader, desc, keywords2);


            MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
            MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(viewProjection));
            MemoryMarshal.Write(bufferData.Slice(128), material.Metallic);
            MemoryMarshal.Write(bufferData.Slice(128 + 4), material.Roughness);
            MemoryMarshal.Write(bufferData.Slice(128 + 8), material.Emissive);
            MemoryMarshal.Write(bufferData.Slice(128 + 12), material.Specular);
            MemoryMarshal.Write(bufferData.Slice(144), material.AO);
            MemoryMarshal.Write(bufferData.Slice(144 + 4), CameraLeft);
            MemoryMarshal.Write(bufferData.Slice(160), CameraDown);
            context.SetCBV(1, bufferData);

            context.SetSRV(0, material._Albedo);
            context.SetSRV(1, material._Metallic);
            context.SetSRV(2, material._Roughness);
            context.SetSRV(3, material._Emissive);
            context.SetSRV(4, material._Normal);
            context.SetSRV(5, material._Spa);

            context.Draw(renderable);
            keywords2.Clear();
        }
    }
}
