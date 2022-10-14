using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.Mathematics;

namespace RenderPipelines;

public class DrawDecalPass : Pass
{
    public string shader;

    public List<(string, string)> keywords = new();
    List<(string, string)> keywords2 = new();

    public PSODesc psoDesc;

    public bool enableVS = true;
    public bool enablePS = true;
    public bool enableGS = false;

    public string rs;

    public bool clearRenderTarget = false;
    public bool clearDepth = false;

    public Rectangle? scissorViewport;

    public object[] CBVPerObject;

    public object[] CBVPerPass;

    public Matrix4x4 viewProj;

    public IEnumerable<VisualComponent> Visuals;

    public Func<RenderHelper, VisualComponent, List<(string, string)>, bool> filter;

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
        if (CBVPerPass != null)
        {
            renderHelper.Write(CBVPerPass, writer);
            writer.SetCBV(1);
        }
        BoundingFrustum frustum = new(viewProj);

        keywords2.Clear();
        foreach (var visual in Visuals)
        {
            if (visual.UIShowType != Caprice.Display.UIShowType.Decal)
                continue;

            if (!frustum.Intersects(new BoundingSphere(visual.transform.position, visual.transform.scale.Length())))
                continue;

            if (filter != null && !filter.Invoke(renderHelper, visual, keywords2))
                continue;
            keywords2.AddRange(this.keywords);

            AutoMapKeyword(renderHelper, keywords2, visual.material);

            renderWrap.SetShader(shader, desc, keywords2, enableVS, enablePS, enableGS);

            Matrix4x4 m = visual.transform.GetMatrix() * viewProj;
            Matrix4x4.Invert(m, out var im);
            CBVPerObject[0] = m;
            CBVPerObject[1] = im;

            renderHelper.Write(CBVPerObject, writer, visual.material);
            writer.SetCBV(0);

            renderWrap.SetSRVs(srvs, visual.material);

            renderHelper.DrawCube();
            keywords2.Clear();
        }
    }
}
