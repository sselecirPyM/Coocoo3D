using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderPBRForward
{
    static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
    {
        { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
        { DebugRenderType.Bitangent,"DEBUG_BITANGENT"},
        { DebugRenderType.Depth,"DEBUG_DEPTH"},
        { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
        { DebugRenderType.DiffuseRender,"DEBUG_DIFFUSE_RENDER"},
        { DebugRenderType.Emissive,"DEBUG_EMISSIVE"},
        { DebugRenderType.Normal,"DEBUG_NORMAL"},
        { DebugRenderType.Position,"DEBUG_POSITION"},
        { DebugRenderType.Roughness,"DEBUG_ROUGHNESS"},
        { DebugRenderType.Specular,"DEBUG_SPECULAR"},
        { DebugRenderType.SpecularRender,"DEBUG_SPECULAR_RENDER"},
        { DebugRenderType.Tangent,"DEBUG_TANGENT"},
        { DebugRenderType.UV,"DEBUG_UV"},
    };
    public static bool UnionShader(UnionShaderParam param)
    {
        var mainCaches = param.mainCaches;
        PSO pso = null;
        var graphicsContext = param.graphicsContext;

        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;

        switch (param.passName)
        {
            case "DrawObjectPass":
            case "DrawTransparentPass":
                param.WriteCBV(param.pass.CBVs[1]);
                string forwardShaderPath = Path.GetFullPath("PBRMaterial.hlsl", param.relativePath);
                foreach (var renderable in param.MeshRenderables())
                {
                    var material = renderable.material;
                    bool transparent = (bool?)param.GetSettingsValue(material, "Transparent") == true;
                    if (param.passName == "DrawTransparentPass" && !transparent) continue;

                    var psoDesc = param.GetPSODesc();
                    bool receiveShadow = (bool)param.GetSettingsValue(material, "ReceiveShadow");

                    List<ValueTuple<string, string>> keywords = new();
                    if (!transparent)
                        psoDesc.blendState = BlendState.None;
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(new(debugKeyword,"1"));
                    if ((bool)param.GetSettingsValue("EnableFog"))
                        keywords.Add(new("ENABLE_FOG", "1"));
                    if ((bool)param.GetSettingsValue(material, "UseNormalMap"))
                        keywords.Add(new("USE_NORMAL_MAP", "1"));

                    if ((bool?)param.GetSettingsValue("UseGI") == true)
                        keywords.Add(new("ENABLE_GI", "1"));
                    if (renderable.gpuSkinning)
                    {
                        keywords.Add(new("SKINNING", "1"));
                        graphicsContext.SetCBVRSlot(param.GetBoneBuffer(), 0, 0, 0);
                    }

                    if (directionalLights.Count != 0)
                    {
                        if (!receiveShadow)
                            keywords.Add(new("DISBLE_SHADOW_RECEIVE", "1"));
                        keywords.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
                    }
                    if (pointLights.Count != 0)
                    {
                        keywords.Add(new("ENABLE_POINT_LIGHT", "1"));
                        keywords.Add(new("POINT_LIGHT_COUNT", pointLights.Count.ToString()));
                    }

                    pso = mainCaches.GetPSOWithKeywords(keywords, forwardShaderPath);
                    //foreach (var cbv in param.pass.CBVs)
                    //{
                    //    param.WriteCBV(cbv);
                    //}
                    param.WriteCBV(param.pass.CBVs[0]);
                    param.SetSRVs(param.pass.SRVs, material);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        param.DrawRenderable(renderable);
                }
                break;
            case "DrawSkyBoxPass":
                {
                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    var psoDesc = param.GetPSODesc();
                    psoDesc.wireFrame = false;
                    pso = mainCaches.GetPSOWithKeywords(null, Path.GetFullPath("SkyBox.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                    {
                        param.DrawQuad();
                    }
                }
                break;
            default:
                return false;
        }
        return true;
    }
}