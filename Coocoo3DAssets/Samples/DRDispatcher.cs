using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public class DRDispatcher : IPassDispatcher
{
    public void FrameBegin(RenderPipelineContext context)
    {
    }

    public void FrameEnd(RenderPipelineContext context)
    {

    }

    public void Dispatch(UnionShaderParam param)
    {
        var passSetting = param.passSetting;
        var graphicsContext = param.graphicsContext;
        var renderers = param.renderers;

        var mainCaches = param.mainCaches;
        param.texLoading = mainCaches.GetTextureLoaded(Path.GetFullPath("loading.png", param.relativePath), graphicsContext);
        param.texError = mainCaches.GetTextureLoaded(Path.GetFullPath("error.png", param.relativePath), graphicsContext);

        bool rayTracing = false;
        bool rayTracingReflect = true;
        bool rayTracingGI = true;
        foreach (var renderSequence in passSetting.RenderSequence)
            if (renderSequence.Type == "RayTracing")
                rayTracing = true;

        if ((bool?)param.GetSettingsValue("EnableRayTracing") != true)
            rayTracingReflect = false;

        if ((bool?)param.GetSettingsValue("UpdateGI") != true)
            rayTracingGI = false;

        rayTracing = rayTracing && (rayTracingReflect || rayTracingGI) && param.IsRayTracingSupport;

        rayTracingReflect &= rayTracing;
        rayTracingGI &= rayTracing;

        param.SetCustomValue("RayTracing", rayTracing);
        param.SetCustomValue("RayTracingReflect", rayTracingReflect);
        param.CPUSkinning = rayTracing;
        var random = param.random;
        if ((bool?)param.GetSettingsValue("EnableTAA") == true)
        {
            var outputTex = param.GetTex2D("_Output0");
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputTex.width, (float)(random.NextDouble() * 2 - 1) / outputTex.height);
            param.visualChannel.cameraData = param.visualChannel.camera.GetCameraData(jitterVector);
        }
        int pointLightSplit = 2;
        for (int i = 4; i * i < param.pointLights.Count * 12; i *= 2)
            pointLightSplit = i;
        pointLightSplit *= 2;
        param.SetGPUValueOverride("LightMapSplit", pointLightSplit);

        foreach (var renderSequence in param.RenderSequences())
        {
            if (renderSequence.Type == "RayTracing" && !rayTracing)
            {
                continue;
            }

            param.DispatchPass(renderSequence);
        }
    }
}