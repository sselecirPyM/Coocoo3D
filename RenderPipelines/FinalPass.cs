using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class FinalPass
{
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

    public PipelineMaterial pipelineMaterial;

    public void Execute(RenderHelper context)
    {
        RenderWrap renderWrap = context.renderWrap;
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


        context.SetSRV<PointLightData>(11, CollectionsMarshal.AsSpan(pointLightDatas));


        //new object[]
        //{
        //        nameof(ViewProjection),
        //        nameof(InvertViewProjection),
        //        nameof(Far),
        //        nameof(Near),
        //        nameof(Fov),
        //        nameof(AspectRatio),
        //        nameof(CameraPosition),
        //        nameof(SkyLightMultiple),
        //        nameof(FogColor),
        //        nameof(FogDensity),
        //        nameof(FogStartDistance),
        //        nameof(FogEndDistance),
        //        nameof(OutputSize),
        //        nameof(Brightness),
        //        nameof(VolumetricLightingSampleCount),
        //        nameof(VolumetricLightingDistance),
        //        nameof(VolumetricLightingIntensity),
        //        nameof(ShadowMapVP),
        //        nameof(ShadowMapVP1),
        //        nameof(LightDir),
        //        0,
        //        nameof(LightColor),
        //        0,
        //        nameof(GIVolumePosition),
        //        nameof(AODistance),
        //        nameof(GIVolumeSize),
        //        nameof(AOLimit),
        //        nameof(AORaySampleCount),
        //        nameof(RandomI),
        //        nameof(Split),
        //},

        var writer = context.Writer;
        if (cbvs != null)
            for (int i = 0; i < cbvs.Length; i++)
            {
                object[] cbv1 = cbvs[i];
                if (cbv1 == null)
                    continue;
                context.Write(cbv1, writer);
                writer.SetCBV(i);
            }
        context.DrawQuad();
        writer.Clear();
        _keywords.Clear();
    }

    void WriteCBuffer(Span<byte> data)
    {

    }
}
