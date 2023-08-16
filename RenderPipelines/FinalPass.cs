using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Collections.Generic;

namespace RenderPipelines;

public class FinalPass
{
    public string[] srvs;

    public string[] renderTargets;


    PSODesc GetPSODesc(RenderHelper renderHelper, PSODesc desc)
    {
        desc.rtvFormat = (renderTargets != null && renderTargets.Length > 0) ?
            renderHelper.renderWrap.GetRenderTexture2D(renderTargets[0]).GetFormat() : Vortice.DXGI.Format.Unknown;
        desc.dsvFormat = Vortice.DXGI.Format.Unknown;
        desc.renderTargetCount = (renderTargets != null) ? renderTargets.Length : 0;

        return desc;
    }

    public string shader = "DeferredFinal.hlsl";

    public List<(string, string)> keywords = new();
    List<(string, string)> _keywords = new();

    public PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
    };

    public object[][] cbvs;

    public bool clearRenderTarget = true;
    public bool clearDepth = false;


    public bool EnableFog;
    public bool EnableSSAO;
    public bool EnableSSR;
    public bool UseGI;
    public bool NoBackGround;

    public void Execute(RenderHelper renderHelper)
    {

        RenderWrap renderWrap = renderHelper.renderWrap;
        _keywords.Clear();
        _keywords.AddRange(this.keywords);

        if (EnableFog)
            _keywords.Add(("ENABLE_FOG", "1"));
        if (EnableSSAO)
            _keywords.Add(("ENABLE_SSAO", "1"));
        if (EnableSSR)
            _keywords.Add(("ENABLE_SSR", "1"));
        if (UseGI)
            _keywords.Add(("ENABLE_GI", "1"));
        if (NoBackGround)
            _keywords.Add(("DISABLE_BACKGROUND", "1"));

        //AutoMapKeyword(renderHelper, _keywords, null);

        renderWrap.SetRenderTarget(renderTargets, null, clearRenderTarget, clearDepth);
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
