﻿using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines;

public class TAAPass
{
    public Texture2D target;
    public Texture2D depth;

    public Texture2D history;
    public Texture2D historyDepth;


    [UIShow(name: "启用TAA抗锯齿")]
    public bool EnableTAA;

    [UIDragFloat(0.01f, name: "混合系数")]
    public float TAAFactor = 0.3f;

    object[] cbv =
    {
        null,//nameof(ViewProjection),
        null,//nameof(InvertViewProjection),
        null,//nameof(_ViewProjection),
        null,//nameof(_InvertViewProjection),
        null,//nameof(outputWidth),
        null,//nameof(outputHeight),
        null,//nameof(cameraFar),
        null,//nameof(cameraNear),
        null,//nameof(TAAFactor),
    };
    public RenderHelper context;
    public DebugRenderType DebugRenderType;


    public void Execute(CameraData historyCamera, CameraData camera)
    {
        Keyword_shader_TAA flags = new Keyword_shader_TAA();
        cbv[0] = camera.vpMatrix;
        cbv[1] = camera.pvMatrix;
        cbv[2] = historyCamera.vpMatrix;
        cbv[3] = historyCamera.pvMatrix;
        cbv[4] = target.width;
        cbv[5] = target.height;
        cbv[6] = camera.far;
        cbv[7] = camera.near;
        cbv[8] = TAAFactor;


        if (DebugRenderType == DebugRenderType.TAA)
            flags |= Keyword_shader_TAA.DEBUG_TAA;


        var writer = context.Writer;
        context.Write(cbv, writer);
        writer.SetCBV(0);

        context.SetSRVs(depth, history, historyDepth);
        context.SetUAV(0, target);
        context.SetPSO(shader_TAA.Get(flags));
        context.Dispatch((target.width + 7) / 8, (target.height + 7) / 8);
    }

    VariantComputeShader<Keyword_shader_TAA> shader_TAA = new VariantComputeShader<Keyword_shader_TAA>(
"""
float _pow2(float x)
{
    return x * x;
}
cbuffer cb0 : register(b0)
{
    float4x4 g_mWorldToProj;
    float4x4 g_mProjToWorld;
    float4x4 g_mWorldToProj1;
    float4x4 g_mProjToWorld1;
    int2 _widthHeight;
    float g_cameraFarClip;
    float g_cameraNearClip;
    float mixFactor;
};
Texture2D _depth : register(t0);
Texture2D _previousResult : register(t1);
Texture2D _previousDepth : register(t2);
SamplerState s0 : register(s0);
SamplerState s3 : register(s3);

RWTexture2D<float4> _result : register(u0);

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
    float2 uv = ((float2) dtid.xy + 0.5) / (float2) _widthHeight;
    float2 reproj = uv * 2 - 1;
    reproj.y = -reproj.y;

    float2 pixelSize = 1.0f / _widthHeight;

    float4 sourceColor = _result[dtid.xy];
    float3 color = sourceColor.rgb;

    float weight = 1;

    float depth2 = _depth.SampleLevel(s0, uv, 0).r;

    float4 wPos2 = mul(float4(reproj, depth2, 1), g_mProjToWorld);
    wPos2 /= wPos2.w;
    float4 posX2 = mul(wPos2, g_mWorldToProj1);
    float2 uv2 = posX2.xy / posX2.w;
    uv2.x = uv2.x * 0.5 + 0.5;
    uv2.y = 0.5 - uv2.y * 0.5;
    bool aa = false;
    float minz = 1;
    float maxz = 0;
    float minz1 = 1;
    float maxz1 = 0;
    float threshold = (100 + _pow2(depth2) * 800 + g_cameraNearClip * 10) / _widthHeight.y;
    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            float depth = _depth.SampleLevel(s0, uv + float2(x, y) * pixelSize, 0).r;
            minz = min(minz, depth);
            maxz = max(maxz, depth);
            float4 wPos = mul(float4(reproj + float2(x, y) * pixelSize * 2, depth, 1), g_mProjToWorld);
            wPos /= wPos.w;

            float4 posX1 = mul(wPos, g_mWorldToProj1);
            float2 uv1 = posX1.xy / posX1.w;
            uv1.x = uv1.x * 0.5 + 0.5;
            uv1.y = 0.5 - uv1.y * 0.5;

            float depth1 = _previousDepth.SampleLevel(s0, uv2 + float2(x, y) * pixelSize, 0).r;
            float4 wPos1 = mul(float4(posX1.xy / posX1.w, depth1, 1), g_mProjToWorld1);
            wPos1 /= wPos1.w;

            float4 projX = mul(wPos1, g_mWorldToProj);
            float depth1X = projX.z / projX.w;
            minz1 = min(minz1, depth1X);
            maxz1 = max(maxz1, depth1X);

            if (distance(wPos.xyz, wPos1.xyz) < threshold)
            {
                aa = true;
            }
        }
    float mid1 = (minz1 + maxz1) / 2;
    if (mid1 > maxz || mid1 < minz)
    {
        aa = false;
    }
    if (aa)
    {
        color *= mixFactor;
        weight *= mixFactor;
        color += _previousResult.SampleLevel(s0, uv2, 0).rgb;
        weight += 1;
    }
    color /= weight;
#if DEBUG_TAA
	if (weight == 1)
	{
		_result[dtid.xy] = float4(0.75, 0.5, 0.75, 1);
		return;
	}
#endif

    _result[dtid.xy] = float4(color, sourceColor.a);
}
""", "csmain");

    public void Dispose()
    {
        shader_TAA?.Dispose();
        shader_TAA = null;
    }

    [Flags]
    enum Keyword_shader_TAA
    {
        None = 0,
        DEBUG_TAA = 1,
    }
}
