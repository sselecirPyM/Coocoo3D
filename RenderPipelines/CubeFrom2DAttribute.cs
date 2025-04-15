using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines;

internal class CubeFrom2DAttribute : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
{
    public bool Bake(Texture2D texture, RenderPipelineView view, ref object tag)
    {
        var input = view.GetRenderTexture2D(Source);
        var graphicsContext = view.graphicsContext;
        if (input == null || input.Status != GraphicsObjectStatus.loaded)
            return false;

        int width = texture.width;
        int height = texture.height;

        graphicsContext.SetPSO(shader_ConvertToCube);
        graphicsContext.SetComputeResources(s =>
        {
            s.SetCBV(0, [width, height]);
            s.SetSRV(0, input);
            s.SetUAVMip(0, texture, 0);
        });
        graphicsContext.Dispatch(width / 8, height / 8, 6);

        int x = width;
        int y = height;
        graphicsContext.SetPSO(shader_GenerateCubeMipMap);
        for (int i = 1; i < texture.mipLevels; i++)
        {
            x /= 2;
            y /= 2;
            graphicsContext.SetComputeResources(s =>
            {
                s.SetCBV(0, [x, y]);
                s.SetSRVMip(0, texture, i - 1);
                s.SetUAVMip(0, texture, i);
            });
            graphicsContext.Dispatch(x / 8, y / 8, 6);
        }
        return true;
    }

    public CubeFrom2DAttribute(string source)
    {
        Source = source;
    }

    public VariantComputeShader shader_ConvertToCube = new VariantComputeShader(
"""
cbuffer cb0 : register(b0)
{
    uint2 imageSize;
}

const static float MATH_PI = 3.141592653589793238;
const static float InvPI = 1 / MATH_PI;
float2 ComputeSphereCoord(float3 normal)
{
    normal = clamp(normal, -1.0, 1.0);
    float2 coord = float2((atan2(-normal.x, normal.z) * InvPI * 0.5f + 0.5f), acos(normal.y) * InvPI);
    return coord;
}
RWTexture2DArray<float4> CubeMap : register(u0);
Texture2D Panorama : register(t0);
SamplerState s0 : register(s0);

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
    float3 N = float3(0, 0, 0);
    float2 screenPos = ((float2) dtid.xy + 0.5f) / (float2) imageSize * 2 - 1;
    if (dtid.x > imageSize.x || dtid.y > imageSize.y)
    {
        return;
    }
    if (dtid.z == 0)
    {
        N = float3(1, -screenPos.y, -screenPos.x);
    }
    else if (dtid.z == 1)
    {
        N = float3(-1, -screenPos.y, screenPos.x);
    }
    else if (dtid.z == 2)
    {
        N = float3(screenPos.x, 1, screenPos.y);
    }
    else if (dtid.z == 3)
    {
        N = float3(screenPos.x, -1, -screenPos.y);
    }
    else if (dtid.z == 4)
    {
        N = float3(screenPos.x, -screenPos.y, 1);
    }
    else
    {
        N = float3(-screenPos.x, -screenPos.y, -1);
    }
    N = normalize(N);
    CubeMap[dtid] = float4(Panorama.SampleLevel(s0, ComputeSphereCoord(N), 0).rgb, 0);
}
""", "csmain");

    public VariantComputeShader shader_GenerateCubeMipMap = new VariantComputeShader(
"""
cbuffer cb0 : register(b0)
{
	uint2 imageSize;
}

const static float COO_PI = 3.141592653589793238;

float4 Pow4(float4 x)
{
	return x * x * x * x;
}
float3 Pow4(float3 x)
{
	return x * x * x * x;
}
float2 Pow4(float2 x)
{
	return x * x * x * x;
}
float Pow4(float x)
{
	return x * x * x * x;
}
float4 Pow2(float4 x)
{
	return x * x;
}
float3 Pow2(float3 x)
{
	return x * x;
}
float2 Pow2(float2 x)
{
	return x * x;
}
float Pow2(float x)
{
	return x * x;
}

float3x3 GetTangentBasis(float3 TangentZ)
{
	const float Sign = TangentZ.z >= 0 ? 1 : -1;
	const float a = -rcp(Sign + TangentZ.z);
	const float b = TangentZ.x * TangentZ.y * a;

	float3 TangentX = { 1 + Sign * a * Pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * Pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

RWTexture2DArray<float4> IrradianceMap : register(u0);
TextureCube Image : register(t0);
SamplerState s0 : register(s0);
[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float3(0, 0, 0);
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)imageSize * 2 - 1;
	if (dtid.x > imageSize.x || dtid.y > imageSize.y)
	{
		return;
    }
    if (dtid.z == 0)
    {
        N = float3(1, -screenPos.y, -screenPos.x);
    }
    else if (dtid.z == 1)
    {
        N = float3(-1, -screenPos.y, screenPos.x);
    }
    else if (dtid.z == 2)
    {
        N = float3(screenPos.x, 1, screenPos.y);
    }
    else if (dtid.z == 3)
    {
        N = float3(screenPos.x, -1, -screenPos.y);
    }
    else if (dtid.z == 4)
    {
        N = float3(screenPos.x, -screenPos.y, 1);
    }
    else
    {
        N = float3(-screenPos.x, -screenPos.y, -1);
    }
	IrradianceMap[dtid] = Image.SampleLevel(s0, N, 0);
}
""", "csmain");

    public string Source { get; }

    public void Dispose()
    {
        shader_ConvertToCube?.Dispose();
        shader_ConvertToCube = null;
        shader_GenerateCubeMipMap?.Dispose();
        shader_GenerateCubeMipMap = null;
    }
}



//internal class CubeFrom2DTexture : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
//{
//    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
//    {
//        var tex = renderWrap.GetTex2D(Source);
//        if (tex == null || tex.Status != GraphicsObjectStatus.loaded)
//            return false;

//        int width = texture.width;
//        int height = texture.height;

//        renderWrap.graphicsContext.SetCBVRSlot<int>(0, [width, height]);

//        renderWrap.SetSRV(0, tex);
//        renderWrap.SetUAV(0, texture, 0);
//        renderWrap.SetPSO(shader_ConvertToCube);
//        renderWrap.Dispatch(width / 8, height / 8, 6);

//        int x = width;
//        int y = height;
//        renderWrap.SetPSO(shader_GenerateCubeMipMap);
//        for (int i = 1; i < texture.mipLevels; i++)
//        {
//            x /= 2;
//            y /= 2;
//            renderWrap.SetSRVMip(0, texture, i - 1);
//            renderWrap.SetUAV(0, texture, i);

//            renderWrap.graphicsContext.SetCBVRSlot<int>(0, [x, y]);
//            renderWrap.Dispatch(x / 8, y / 8, 6);
//        }
//        return true;
//    }
//    bool ready;
//    public VariantComputeShader shader_ConvertToCube = new VariantComputeShader(
//"""
//cbuffer cb0 : register(b0)
//{
//    uint2 imageSize;
//}

//const static float MATH_PI = 3.141592653589793238;
//const static float InvPI = 1 / MATH_PI;
//float2 ComputeSphereCoord(float3 normal)
//{
//    normal = clamp(normal, -1.0, 1.0);
//    float2 coord = float2((atan2(-normal.x, normal.z) * InvPI * 0.5f + 0.5f), acos(normal.y) * InvPI);
//    return coord;
//}
//RWTexture2DArray<float4> CubeMap : register(u0);
//Texture2D Panorama : register(t0);
//SamplerState s0 : register(s0);

//[numthreads(8, 8, 1)]
//void csmain(uint3 dtid : SV_DispatchThreadID)
//{
//    float3 N = float3(0, 0, 0);
//    float2 screenPos = ((float2) dtid.xy + 0.5f) / (float2) imageSize * 2 - 1;
//    if (dtid.x > imageSize.x || dtid.y > imageSize.y)
//    {
//        return;
//    }
//    if (dtid.z == 0)
//    {
//        N = float3(1, -screenPos.y, -screenPos.x);
//    }
//    else if (dtid.z == 1)
//    {
//        N = float3(-1, -screenPos.y, screenPos.x);
//    }
//    else if (dtid.z == 2)
//    {
//        N = float3(screenPos.x, 1, screenPos.y);
//    }
//    else if (dtid.z == 3)
//    {
//        N = float3(screenPos.x, -1, -screenPos.y);
//    }
//    else if (dtid.z == 4)
//    {
//        N = float3(screenPos.x, -screenPos.y, 1);
//    }
//    else
//    {
//        N = float3(-screenPos.x, -screenPos.y, -1);
//    }
//    N = normalize(N);
//    CubeMap[dtid] = float4(Panorama.SampleLevel(s0, ComputeSphereCoord(N), 0).rgb, 0);
//}
//""", "csmain");

//    public VariantComputeShader shader_GenerateCubeMipMap = new VariantComputeShader(
//"""
//cbuffer cb0 : register(b0)
//{
//	uint2 imageSize;
//}

//const static float COO_PI = 3.141592653589793238;

//float4 Pow4(float4 x)
//{
//	return x * x * x * x;
//}
//float3 Pow4(float3 x)
//{
//	return x * x * x * x;
//}
//float2 Pow4(float2 x)
//{
//	return x * x * x * x;
//}
//float Pow4(float x)
//{
//	return x * x * x * x;
//}
//float4 Pow2(float4 x)
//{
//	return x * x;
//}
//float3 Pow2(float3 x)
//{
//	return x * x;
//}
//float2 Pow2(float2 x)
//{
//	return x * x;
//}
//float Pow2(float x)
//{
//	return x * x;
//}

//float3x3 GetTangentBasis(float3 TangentZ)
//{
//	const float Sign = TangentZ.z >= 0 ? 1 : -1;
//	const float a = -rcp(Sign + TangentZ.z);
//	const float b = TangentZ.x * TangentZ.y * a;

//	float3 TangentX = { 1 + Sign * a * Pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
//	float3 TangentY = { b,  Sign + a * Pow2(TangentZ.y), -TangentZ.y };

//	return float3x3(TangentX, TangentY, TangentZ);
//}

//float3 TangentToWorld(float3 Vec, float3 TangentZ)
//{
//	return mul(Vec, GetTangentBasis(TangentZ));
//}

//RWTexture2DArray<float4> IrradianceMap : register(u0);
//TextureCube Image : register(t0);
//SamplerState s0 : register(s0);
//[numthreads(8, 8, 1)]
//void csmain(uint3 dtid : SV_DispatchThreadID)
//{
//	float3 N = float3(0, 0, 0);
//	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)imageSize * 2 - 1;
//	if (dtid.x > imageSize.x || dtid.y > imageSize.y)
//	{
//		return;
//    }
//    if (dtid.z == 0)
//    {
//        N = float3(1, -screenPos.y, -screenPos.x);
//    }
//    else if (dtid.z == 1)
//    {
//        N = float3(-1, -screenPos.y, screenPos.x);
//    }
//    else if (dtid.z == 2)
//    {
//        N = float3(screenPos.x, 1, screenPos.y);
//    }
//    else if (dtid.z == 3)
//    {
//        N = float3(screenPos.x, -1, -screenPos.y);
//    }
//    else if (dtid.z == 4)
//    {
//        N = float3(screenPos.x, -screenPos.y, 1);
//    }
//    else
//    {
//        N = float3(-screenPos.x, -screenPos.y, -1);
//    }
//	IrradianceMap[dtid] = Image.SampleLevel(s0, N, 0);
//}
//""", "csmain");

//    public string Source { get; }

//    public void Dispose()
//    {
//        shader_ConvertToCube?.Dispose();
//        shader_ConvertToCube = null;
//        shader_GenerateCubeMipMap?.Dispose();
//        shader_GenerateCubeMipMap = null;
//    }
//}
