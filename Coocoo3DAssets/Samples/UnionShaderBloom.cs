using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderBloom
{
    public static bool UnionShader(UnionShaderParam param)
    {
        if ((bool?)param.GetSettingsValue("EnableBloom") != true) return true;
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;
        var psoDesc = param.GetPSODesc();
        psoDesc.wireFrame = false;

        var writer = param.GPUWriter;
        Texture2D renderTarget = param.renderTargets[0];
        writer.Write(renderTarget.width);
        writer.Write(renderTarget.height);
        writer.Write((float)param.GetSettingsValue("BloomThreshold"));
        writer.Write((float)param.GetSettingsValue("BloomIntensity"));
        writer.SetBufferImmediately(0);

        PSO pso = null;
        List<ValueTuple<string, string>> keywords = new();
        switch (param.passName)
        {
            case "BloomBlur1":
                {
                    keywords.Add(new("BLOOM_1", "1"));
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            case "BloomBlur2":
                {
                    keywords.Add(new("BLOOM_2", "1"));
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            default:
                return false;
        }
        if (param.settings.DebugRenderType == DebugRenderType.Bloom)
        {
            psoDesc.blendState = BlendState.None;
        }
        param.SetSRVs(param.pass.SRVs);
        if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
            param.DrawQuad();
        return true;
    }
}