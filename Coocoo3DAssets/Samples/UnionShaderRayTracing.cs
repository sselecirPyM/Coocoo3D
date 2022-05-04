using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public static class UnionShaderRayTracing
{
    static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
    {
        { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
        { DebugRenderType.AO,"DEBUG_AO"},
        { DebugRenderType.Depth,"DEBUG_DEPTH"},
        { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
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
        var renderers = param.renderers;
        var camera = param.visualChannel.cameraData;
        var passSetting = param.passSetting;
        var rayTracingShader = mainCaches.GetRayTracingShader(passSetting.GetAliases(param.pass.RayTracingShader));
        switch (param.passName)
        {
            case "RayTracingPass":
                List<ValueTuple<string, string>> keywords = new();
                if (directionalLights.Count != 0)
                {
                    keywords.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
                    if ((bool)param.GetSettingsValue("EnableVolumetricLighting"))
                        keywords.Add(new("ENABLE_VOLUME_LIGHTING", "1"));
                }
                if ((bool)param.GetSettingsValue("UseGI"))
                    keywords.Add(new("ENABLE_GI", "1"));
                var rtpso = param.mainCaches.GetRTPSO(keywords,
                rayTracingShader,
                Path.GetFullPath(rayTracingShader.hlslFile, param.relativePath));

                if (!graphicsContext.SetPSO(rtpso)) return false;
                var CBVs = param.pass.CBVs;
                var tpas = new RTTopLevelAcclerationStruct();
                tpas.instances = new();
                foreach (var renderable in param.MeshRenderables(false))
                {
                    var material = renderable.material;
                    var psoDesc = param.GetPSODesc();
                    var btas = new RTBottomLevelAccelerationStruct();

                    btas.mesh = renderable.mesh;
                    btas.meshOverride = renderable.meshOverride;
                    btas.indexStart = renderable.indexStart;
                    btas.indexCount = renderable.indexCount;
                    btas.vertexStart = renderable.vertexStart;
                    btas.vertexCount = renderable.vertexCount;
                    var inst = new RTInstance() { accelerationStruct = btas };
                    inst.transform = renderable.transform;
                    inst.hitGroupName = "rayHit";
                    inst.SRVs = new();
                    inst.SRVs.Add(4, param.GetTex2DFallBack("_Albedo", material));
                    inst.SRVs.Add(5, param.GetTex2DFallBack("_Emissive", material));
                    inst.SRVs.Add(6, param.GetTex2DFallBack("_Metallic", material));
                    inst.SRVs.Add(7, param.GetTex2DFallBack("_Roughness", material));
                    inst.CBVs = new();
                    inst.CBVs.Add(0, param.GetCBVData(CBVs[1]));
                    tpas.instances.Add(inst);
                }

                Texture2D renderTarget = param.renderTargets[0];
                int width = renderTarget.width;
                int height = renderTarget.height;

                RayTracingCall call = new RayTracingCall();
                call.tpas = tpas;
                call.UAVs = new();
                param.SRVUAVs(param.pass.UAVs, call.UAVs);
                call.SRVs = new();
                param.SRVUAVs(param.pass.SRVs, call.SRVs, call.srvFlags);

                call.CBVs = new();
                call.CBVs.Add(0, param.GetCBVData(CBVs[0]));
                call.missShaders = new[] { "miss" };

                if ((bool)param.GetSettingsValue("UpdateGI"))
                {
                    call.rayGenShader = "rayGenGI";
                    graphicsContext.DispatchRays(16, 16, 16, call);
                    param.SwapBuffer("GIBuffer", "GIBufferWrite");
                }
                if ((bool)param.GetSettingsValue("EnableRayTracing"))
                {
                    call.rayGenShader = "rayGen";
                    graphicsContext.DispatchRays(width, height, 1, call);
                }

                foreach (var inst in tpas.instances)
                    inst.accelerationStruct.Dispose();
                tpas.Dispose();
                break;
            default:
                return false;
        }
        return true;
    }
}