using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderDeferred
{
    static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
    {
        { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
        { DebugRenderType.AO,"DEBUG_AO"},
        { DebugRenderType.Depth,"DEBUG_DEPTH"},
        { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
        { DebugRenderType.DiffuseProbes,"DEBUG_DIFFUSE_PROBES"},
        { DebugRenderType.DiffuseRender,"DEBUG_DIFFUSE_RENDER"},
        { DebugRenderType.Emissive,"DEBUG_EMISSIVE"},
        { DebugRenderType.Normal,"DEBUG_NORMAL"},
        { DebugRenderType.Position,"DEBUG_POSITION"},
        { DebugRenderType.Roughness,"DEBUG_ROUGHNESS"},
        { DebugRenderType.Specular,"DEBUG_SPECULAR"},
        { DebugRenderType.SpecularRender,"DEBUG_SPECULAR_RENDER"},
        { DebugRenderType.UV,"DEBUG_UV"},
    };
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;
        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;
        PSO pso = null;
        switch (param.passName)
        {
            case "GBufferPass":
                string gbufferShaderPath = Path.GetFullPath("DeferredGBuffer.hlsl", param.relativePath);
                foreach (var renderable in param.MeshRenderables())
                {
                    List<ValueTuple<string, string>> keywords = new();
                    var material = renderable.material;
                    bool transparent = (bool?)param.GetSettingsValue(material, "Transparent") == true;
                    if (transparent) continue;
                    var psoDesc = param.GetPSODesc();
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(new(debugKeyword, "1"));
                    if ((bool)param.GetSettingsValue(material, "UseNormalMap"))
                        keywords.Add(new("USE_NORMAL_MAP", "1"));
                    if (renderable.gpuSkinning)
                    {
                        keywords.Add(new("SKINNING", "1"));
                        graphicsContext.SetCBVRSlot(param.GetBoneBuffer(), 0, 0, 0);
                    }
                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    pso = mainCaches.GetPSOWithKeywords(keywords, gbufferShaderPath);
                    param.SetSRVs(param.pass.SRVs, material);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        param.DrawRenderable(renderable);
                }
                break;
            case "DeferredFinalPass":
                {
                    List<ValueTuple<string, string>> keywords = new();
                    var psoDesc = param.GetPSODesc();
                    psoDesc.wireFrame = false;
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(new(debugKeyword, "1"));
                    if ((bool)param.GetSettingsValue("UseGI"))
                        keywords.Add(new("ENABLE_GI", "1"));
                    if ((bool)param.GetSettingsValue("EnableFog"))
                        keywords.Add(new("ENABLE_FOG", "1"));
                    if ((bool)param.GetSettingsValue("EnableSSAO"))
                        keywords.Add(new("ENABLE_SSAO", "1"));

                    if (directionalLights.Count != 0)
                    {
                        keywords.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
                        if ((bool)param.GetSettingsValue("EnableVolumetricLighting"))
                            keywords.Add(new("ENABLE_VOLUME_LIGHTING", "1"));
                    }
                    if (pointLights.Count != 0)
                    {
                        keywords.Add(new("ENABLE_POINT_LIGHT", "1"));
                        keywords.Add(new("POINT_LIGHT_COUNT", pointLights.Count.ToString()));
                    }
                    if (param.GetCustomValue("RayTracingReflect", false))
                        keywords.Add(new("RAY_TRACING", "1"));

                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("DeferredFinal.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        param.DrawQuad();
                }
                break;
            case "DenoisePass":
                {
                    List<ValueTuple<string, string>> keywords = new();
                    if (!param.GetCustomValue("RayTracingReflect", false))
                        return true;

                    var psoDesc = param.GetPSODesc();
                    psoDesc.wireFrame = false;
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(new(debugKeyword, "1"));

                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("RayTracingDenoise.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        param.DrawQuad();
                }
                break;
            default:
                return false;
        }
        return true;
    }
}