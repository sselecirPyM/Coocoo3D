using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Mathematics;

namespace RenderPipelines;

public class DrawDecalPass : Pass
{
    string shader = "DeferredDecal.hlsl";

    List<(string, string)> keywords2 = new();

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.PreserveAlpha,
        cullMode = CullMode.Front,
    };

    public object[] CBVPerObject;

    public Matrix4x4 viewProj;

    public IEnumerable<VisualComponent> Visuals;

    public override void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;

        var desc = GetPSODesc(renderHelper, psoDesc);

        var writer = renderHelper.Writer;
        writer.Clear();

        BoundingFrustum frustum = new(viewProj);

        foreach (var visual in Visuals)
        {
            if (visual.UIShowType != Caprice.Display.UIShowType.Decal)
                continue;

            ref var transform = ref visual.transform;

            if (!frustum.Intersects(new BoundingSphere(transform.position, transform.scale.Length())))
                continue;

            AutoMapKeyword(renderHelper, keywords2, visual.material);

            renderWrap.SetShader(shader, desc, keywords2);

            Matrix4x4 m = transform.GetMatrix() * viewProj;
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
