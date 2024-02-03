using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class FinalPass
{
    public string[] srvs;

    PSODesc GetPSODesc(RenderWrap renderWrap, PSODesc desc)
    {
        var rtvs = renderWrap.RenderTargets;
        desc.rtvFormat = rtvs[0].GetFormat();
        desc.renderTargetCount = rtvs.Count;

        return desc;
    }

    string shader = "DeferredFinal.hlsl";

    public List<(string, string)> keywords = new();
    List<(string, string)> _keywords = new();

    public PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
        dsvFormat = Vortice.DXGI.Format.Unknown,
    };

    public object[][] cbvs;
    public List<PointLightData> pointLightDatas = new List<PointLightData>();

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

        var desc = GetPSODesc(renderWrap, psoDesc);
        renderWrap.SetShader(shader, desc, _keywords);
        renderWrap.SetSRVs(srvs);

        renderWrap.graphicsContext.SetSRVTSlot<PointLightData>(11, CollectionsMarshal.AsSpan(pointLightDatas));

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
