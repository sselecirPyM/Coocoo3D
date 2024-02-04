using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines;

public class EnvironmentReflectionAttribute : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
{
    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
    {
        var tex = renderWrap.GetTex2D(Source);
        if (tex == null || tex.Status != GraphicsObjectStatus.loaded)
            return false;

        int currentQuality;
        if (tag is int val)
            currentQuality = val;
        else
            currentQuality = 0;

        int width = texture.width;
        int height = texture.height;
        var writer = new GPUWriter();
        writer.graphicsContext = renderWrap.graphicsContext;

        int roughnessLevel = 5;

        {
            int t1 = roughnessLevel + 1;
            int face = 0;

            int mipLevel = currentQuality % t1;
            int quality = currentQuality / t1;
            int pow2a = 1 << mipLevel;
            writer.Write(width / pow2a);
            writer.Write(height / pow2a);
            writer.Write(quality);
            writer.Write(quality);
            writer.Write(Math.Max(mipLevel * mipLevel / (4.0f * 4.0f), 5e-3f));
            writer.Write(face);
            writer.SetCBV(0);

            renderWrap.SetSRV(0, tex);
            renderWrap.SetUAV(0, texture, mipLevel);


            //if (mipLevel != roughnessLevel)
            //    renderWrap.Dispatch("PreFilterEnv.hlsl", null, width / 8 / pow2a, height / 8 / pow2a, 6);
            //else
            //    renderWrap.Dispatch("IrradianceMap.hlsl", null, width / 8 / pow2a, height / 8 / pow2a, 6);


            if (mipLevel != roughnessLevel)
                renderWrap.SetPSO(shader_PreFilterEnv);
            else
                renderWrap.SetPSO(shader_IrradianceMap);

            renderWrap.Dispatch(width / 8 / pow2a, height / 8 / pow2a, 6);

            currentQuality++;
        }
        tag = currentQuality;
        if (currentQuality < 512)
            return false;
        else
            return true;
    }

    public void Dispose()
    {
        shader_IrradianceMap?.Dispose();
        shader_IrradianceMap = null;
        shader_PreFilterEnv?.Dispose();
        shader_PreFilterEnv = null;
    }

    public EnvironmentReflectionAttribute(string source)
    {
        Source = source;
    }
    public string Source { get; }

    public VariantComputeShader shader_IrradianceMap = new VariantComputeShader(
"""
#include "Random.hlsli"
cbuffer cb0 : register(b0)
{
    uint2 imageSize;
    int quality;
    uint batch;
    int notUse;
    int face;
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
    float3 TangentY = { b, Sign + a * Pow2(TangentZ.y), -TangentZ.y };

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
    //int __face = dtid.z;
    int __face = face + dtid.z;
    uint randomState = RNG::RandomSeed(dtid.x + dtid.y * 2048 + __face * 4194304 + batch * 67108864);
    float2 screenPos = ((float2) dtid.xy + 0.5f) / (float2) imageSize * 2 - 1;
    if (dtid.x > imageSize.x || dtid.y > imageSize.y)
    {
        return;
    }
    if (__face == 0)
    {
        N = float3(1, -screenPos.y, -screenPos.x);
    }
    else if (__face == 1)
    {
        N = float3(-1, -screenPos.y, screenPos.x);
    }
    else if (__face == 2)
    {
        N = float3(screenPos.x, 1, screenPos.y);
    }
    else if (__face == 3)
    {
        N = float3(screenPos.x, -1, -screenPos.y);
    }
    else if (__face == 4)
    {
        N = float3(screenPos.x, -screenPos.y, 1);
    }
    else
    {
        N = float3(-screenPos.x, -screenPos.y, -1);
    }
    N = normalize(N);
    float3 col1 = float3(0, 0, 0);
    const int c_sampleCount = 256;
    for (int i = 0; i < c_sampleCount; i++)
    {
        float2 E = Hammersley(i, c_sampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
        float3 vec1 = TangentToWorld(N, HemisphereSampleCos(E));

        float NdotL = dot(vec1, N);
        col1 += Image.SampleLevel(s0, vec1, 2) * NdotL;
    }
    float xd0 = 1 / (float) (quality + 1);
    float xd1 = 1 - xd0;
    IrradianceMap[uint3(dtid.xy, __face)] = float4(col1 / c_sampleCount / 3.14159265359f * xd0 + IrradianceMap[uint3(dtid.xy, __face)].rgb * xd1, 1);
}
""", "csmain", "IrradianceMap.hlsl");

    public VariantComputeShader shader_PreFilterEnv = new VariantComputeShader(
"""
#include "Random.hlsli"
cbuffer cb0 : register(b0)
{
	uint2 imageSize;
	int quality;
	uint batch;
	float roughness1;
	int face;
}

const static float COO_PI = 3.141592653589793238;
RWTexture2DArray<float4> EnvMap : register(u0);
TextureCube AmbientCubemap : register(t0);
SamplerState s0 : register(s0);
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
float4 ImportanceSampleGGX(float2 E, float a2)
{
	float Phi = 2 * COO_PI * E.x;
	float CosTheta = sqrt((1 - E.y) / (1 + (a2 - 1) * E.y));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float d = (CosTheta * a2 - CosTheta) * CosTheta + 1;
	float D = a2 / (COO_PI * d * d);
	float PDF = D * CosTheta;

	return float4(H, PDF);
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

float3 PrefilterEnvMap(uint2 Random, float Roughness, float3 R)
{
	float3 FilteredColor = 0;
	float Weight = 0;

	uint NumSamples = 256;
	if (Roughness < 0.5625)
		NumSamples = 64;
	if (Roughness < 0.25)
		NumSamples = 8;
	if (Roughness < 0.0625)
		NumSamples = 2;
	for (uint i = 0; i < NumSamples; i++)
	{
		float2 E = Hammersley(i, NumSamples, Random);
		float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(Roughness)).xyz, R);
		float3 L = 2 * dot(R, H) * H - R;

		float NoL = saturate(dot(R, L));
		if (NoL > 0)
		{
			FilteredColor += AmbientCubemap.SampleLevel(s0, L, Roughness * 5).rgb * NoL;
			Weight += NoL;
		}
	}

	return FilteredColor / max(Weight, 0.001);
}



[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float4(0, 0, 0, 0);
	uint2 size1 = imageSize;
	//int __face = dtid.z;
	int __face = face + dtid.z;
	uint randomState = RNG::RandomSeed(dtid.x + dtid.y * 2048 + __face * 4194304 + batch * 67108864);
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)size1 * 2 - 1;
	if (dtid.x > size1.x || dtid.y > size1.y)
	{
		return;
	}
	if (__face == 0)
	{
		N = float3(1, -screenPos.y, -screenPos.x);
	}
	else if (__face == 1)
	{
        N = float3(-1, -screenPos.y, screenPos.x);
    }
	else if (__face == 2)
	{
        N = float3(screenPos.x, 1, screenPos.y);
    }
	else if (__face == 3)
	{
        N = float3(screenPos.x, -1, -screenPos.y);
    }
	else if (__face == 4)
	{
        N = float3(screenPos.x, -screenPos.y, 1);
    }
	else
	{
		N = float3(-screenPos.x, -screenPos.y, -1);
	}
	N = normalize(N);
	float xd0 = 1 / (float)(quality + 1);
	float xd1 = quality / (float)(quality + 1);
	EnvMap[uint3(dtid.xy, __face)] = float4(PrefilterEnvMap(uint2(RNG::Random(randomState), RNG::Random(randomState)), roughness1, N) * xd0 + EnvMap[uint3(dtid.xy, __face)].rgb * xd1, 1);
}
""", "csmain", "PreFilterEnv.hlsl");
}
