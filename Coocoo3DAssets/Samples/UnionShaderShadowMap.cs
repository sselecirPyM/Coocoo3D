using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public static class UnionShaderShadowMap
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        int width = param.depthStencil.width;
        int height = param.depthStencil.height;
        if (param.directionalLights.Count > 0)
        {
            if (param.pass.CBVs.Count < 2)
                return false;
            graphicsContext.RSSetScissorRectAndViewport(0, 0, width / 2, height / 2);
            DrawShadow(param, 0);
            graphicsContext.RSSetScissorRectAndViewport(width / 2, 0, width, height / 2);
            DrawShadow(param, 1);
        }
        int pointLightIndex = 0;
        int shadowIndex = 0;
        int split = param.GetGPUValueOverride("LightMapSplit", 1);
        foreach (var pointLight in param.pointLights)
        {
            if (param.pass.CBVs.Count < 3)
                return false;

            float lightRange = pointLight.Range;
            Vector3 lightPos = pointLight.Position;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(1, 0, 0), new Vector3(0, -1, 0), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(-1, 0, 0), new Vector3(0, 1, 0), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(0, 1, 0), new Vector3(0, 0, -1), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(0, -1, 0), new Vector3(0, 0, 1), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(0, 0, 1), new Vector3(-1, 0, 0), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            SetViewport(param, width, height, shadowIndex, split);
            SetMatrix(param, lightPos, new Vector3(0, 0, -1), new Vector3(1, 0, 0), lightRange * 0.001f, lightRange);
            DrawShadow(param, 2);
            shadowIndex++;

            pointLightIndex++;
        }
        return true;
    }

    static void SetMatrix(UnionShaderParam param, Vector3 pos, Vector3 dir, Vector3 up, float near, float far)
    {
        param.SetGPUValueOverride("_PointLightMatrix", Matrix4x4.CreateLookAt(pos, pos + dir, up)
            * Matrix4x4.CreatePerspectiveFieldOfView(1.57079632679f, 1, near, far));
    }

    static void SetViewport(UnionShaderParam param, int width, int height, int shadowIndex, int split)
    {
        var graphicsContext = param.graphicsContext;
        float xOffset = (float)(shadowIndex % split) / split;
        float yOffset = (float)(shadowIndex / split) / split;
        float size = 1.0f / split;

        int x = (int)(width * xOffset);
        int y = (int)(height * (yOffset + 0.5f));
        int sizeX1 = (int)(width * size);
        int sizeY1 = (int)(height * size);
        graphicsContext.RSSetScissorRectAndViewport(x, y, x + sizeX1, y + sizeY1);
    }

    static void DrawShadow(UnionShaderParam param, int cbvIndex)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;

        int width = param.depthStencil.width;
        int height = param.depthStencil.height;

        foreach (var renderable in param.MeshRenderables())
        {
            var material = renderable.material;
            var psoDesc = param.GetPSODesc();
            psoDesc.wireFrame = false;

            if (!(bool)param.GetSettingsValue(material, "CastShadow")) continue;

            List<ValueTuple<string, string>> keywords = new();
            if (renderable.gpuSkinning)
                keywords.Add(new("SKINNING","1"));
            param.WriteCBV(param.pass.CBVs[cbvIndex]);
            //foreach (var cbv in param.pass.CBVs)
            //{
            //    param.WriteCBV(cbv);
            //}
            var pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("ShadowMap.hlsl", param.relativePath), true, false);
            param.SetSRVs(param.pass.SRVs, material);
            if (graphicsContext.SetPSO(pso, psoDesc))
            {
                if (renderable.gpuSkinning)
                {
                    graphicsContext.SetCBVRSlot(param.GetBoneBuffer(), 0, 0, 0);
                }
                param.DrawRenderable(renderable);
            }
        }
    }
}