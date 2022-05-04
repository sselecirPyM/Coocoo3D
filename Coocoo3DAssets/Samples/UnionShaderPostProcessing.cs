using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public static class UnionShaderPostProcessing
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;
        var psoDesc = param.GetPSODesc();
        psoDesc.wireFrame = false;

        PSO pso = null;
        string matName = param.visualChannel.Name + "previousCamera";
        string matInvName = param.visualChannel.Name + "previousCamera1";
        //var camera = param.visualChannel.cameraData;
        var camera = param.visualChannel.camera.GetCameraData();
        var mat = camera.vpMatrix;
        var matInv = camera.pvMatrix;
        List<ValueTuple<string, string>> keywords = new();
        switch (param.passName)
        {
            case "TAAPass":
                {
                    if ((bool?)param.GetSettingsValue("EnableTAA") != true)
                        return true;
                    var mat2 = param.GetPersistentValue(matName, mat);

                    var matInv2 = param.GetPersistentValue(matInvName, matInv);

                    Texture2D renderTarget = param.renderTargets[0];
                    var writer = param.GPUWriter;
                    writer.Write(mat);
                    writer.Write(matInv);
                    writer.Write(mat2);
                    writer.Write(matInv2);
                    writer.Write(renderTarget.width);
                    writer.Write(renderTarget.height);
                    writer.Write(camera.far);
                    writer.Write(camera.near);
                    writer.Write((float)param.GetSettingsValue("TAAFrameFactor"));
                    writer.SetBufferImmediately(0);

                    keywords.Add(new("ENABLE_TAA", "1"));
                    if (param.settings.DebugRenderType == DebugRenderType.TAA)
                    {
                        keywords.Add(new("DEBUG_TAA", "1"));
                    }
                    var computeShader = mainCaches.GetComputeShaderWithKeywords(keywords, Path.GetFullPath("TAA.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs);
                    param.SetUAVs(param.pass.UAVs);
                    if (computeShader != null && graphicsContext.SetPSO(computeShader))
                        graphicsContext.Dispatch((renderTarget.width + 7) / 8, (renderTarget.height + 7) / 8, 1);
                }
                break;
            case "PostProcessingPass":
                {
                    var mat2 = param.GetPersistentValue(matName, mat);
                    param.SetPersistentValue(matName, mat);

                    var matInv2 = param.GetPersistentValue(matInvName, matInv);
                    param.SetPersistentValue(matInvName, matInv);

                    Texture2D renderTarget = param.renderTargets[0];
                    var writer = param.GPUWriter;
                    writer.Write(mat);
                    writer.Write(matInv);
                    writer.Write(mat2);
                    writer.Write(matInv2);
                    writer.Write(renderTarget.width);
                    writer.Write(renderTarget.height);
                    writer.Write(camera.far);
                    writer.Write(camera.near);

                    writer.SetBufferImmediately(0);

                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("PostProcessing.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        param.DrawQuad();
                    if (param.pass.SRVs.Count > 3)
                    {
                        param.SwapTexture("_Result", "_PreviousResult");
                        param.SwapTexture("_ScreenDepth0", "_PreviousScreenDepth0");
                    }
                }
                break;
            default:
                return false;
        }
        return true;
    }
}