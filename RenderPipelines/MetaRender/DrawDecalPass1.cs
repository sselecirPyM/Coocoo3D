using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace RenderPipelines.MetaRender;

public class DrawDecalPass1 : Pass
{
    string shader = "DeferredDecal.hlsl";

    List<(string, string)> keywords2 = new();

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.PreserveAlpha,
        cullMode = CullMode.Front,
    };

    string rs = "CCCssss";

    public bool clearRenderTarget = false;
    public bool clearDepth = false;

    public Rectangle? scissorViewport;

    public Matrix4x4 viewProj;

    public IEnumerable<DecalRenderable> decals;

    public override void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;
        renderWrap.SetRootSignature(rs);
        renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
        if (scissorViewport != null)
        {
            var rect = scissorViewport.Value;
            renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
        var desc = GetPSODesc(renderHelper, psoDesc);

        var writer = renderHelper.Writer;
        writer.Clear();

        keywords2.Clear();
        foreach (var decal in decals)
        {
            var material = decal.material as DecalMaterial;
            if (material == null)
                continue;

            if (material.EnableDecalEmissive)
                keywords2.Add(("ENABLE_DECAL_EMISSIVE", "1"));
            if (material.EnableDecalColor)
                keywords2.Add(("ENABLE_DECAL_COLOR", "1"));

            renderWrap.SetShader(shader, desc, keywords2);

            Matrix4x4 m = decal.transform.GetMatrix() * viewProj;
            Matrix4x4.Invert(m, out var im);

            writer.Write(m);
            writer.Write(im);
            writer.Write(material._DecalEmissivePower);
            writer.SetCBV(0);
            var depth = renderWrap.GetTex2DFallBack(srvs[0]);
            renderWrap.SetSRV(depth, 0);
            renderWrap.SetSRV(material.DecalColorTexture, 1);
            renderWrap.SetSRV(material.DecalEmissiveTexture, 2);
            //renderWrap.SetSRVs(srvs, decal.properties);

            renderHelper.DrawCube();
            keywords2.Clear();
        }
    }
}
