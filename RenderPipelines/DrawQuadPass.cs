using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Collections.Generic;

namespace RenderPipelines;

public class DrawQuadPass : Pass
{
    public string shader;

    public List<(string, string)> keywords = new();
    List<(string, string)> _keywords = new();

    public PSODesc psoDesc;

    public object[][] cbvs;

    public bool clearRenderTarget = false;
    public bool clearDepth = false;

    public override void Execute(RenderHelper renderHelper)
    {

        RenderWrap renderWrap = renderHelper.renderWrap;
        _keywords.Clear();
        _keywords.AddRange(this.keywords);

        AutoMapKeyword(renderHelper, _keywords, null);

        renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
        var desc = GetPSODesc(renderHelper, psoDesc);
        renderWrap.SetShader(shader, desc, _keywords);
        renderWrap.SetSRVs(srvs);

        var writer = renderHelper.Writer;
        if (cbvs != null)
            for (int i = 0; i < cbvs.Length; i++)
            {
                object[] cbv1 = cbvs[i];
                if (cbv1 == null)
                    continue;
                renderHelper.Write(cbv1, writer);
                writer.SetCBV(i);
            }
        renderHelper.DrawQuad();
        writer.Clear();
        _keywords.Clear();
    }
}
