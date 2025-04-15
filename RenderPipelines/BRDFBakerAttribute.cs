using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines;

public class BRDFBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
{
    public bool Bake(Texture2D texture, RenderPipelineView view, ref object tag)
    {
        view.graphicsContext.SetPSO(shader_BRDFLUT);
		view.graphicsContext.SetComputeResources((s) =>
		{
			s.SetCBV(0, [texture.width, texture.height]);
			s.SetUAV(0, texture);
        });
        view.graphicsContext.Dispatch((texture.width + 7) / 8, (texture.height + 7) / 8, 6);
        return true;
    }

    public void Dispose()
    {
        shader_BRDFLUT?.Dispose();
        shader_BRDFLUT = null;
    }


    VariantComputeShader shader_BRDFLUT = new VariantComputeShader(
"""
//ref: https://learnopengl.com/PBR/IBL/Specular-IBL
#include "Random.hlsli"
#include "PBR.hlsli"
#ifndef PI
#define PI 3.14159265358979
#endif

float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
	float NdotV = max(dot(N, V), 0.0);
	float NdotL = max(dot(N, L), 0.0);
	float ggx2 = GeometrySchlickGGX(NdotV, roughness);
	float ggx1 = GeometrySchlickGGX(NdotL, roughness);

	return ggx1 * ggx2;
}

float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
{
	float a = roughness * roughness;

	float phi = 2.0 * PI * Xi.x;
	float cosTheta = saturate(sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y)));
	float sinTheta = saturate(sqrt(1.0 - cosTheta * cosTheta));

	// from spherical coordinates to cartesian coordinates
	float3 H;
	H.x = cos(phi) * sinTheta;
	H.y = sin(phi) * sinTheta;
	H.z = cosTheta;

	// from tangent-space vector to world-space sample vector
	float3 up = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
	float3 tangent = normalize(cross(up, N));
	float3 bitangent = cross(N, tangent);

	float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
	return normalize(sampleVec);
}

float2 IntegrateBRDF(float NdotV, float roughness)
{
	float3 V;
	V.x = sqrt(1.0 - NdotV * NdotV);
	V.y = 0.0;
	V.z = NdotV;

	float A = 0.0;
	float B = 0.0;

	float3 N = float3(0.0, 0.0, 1.0);

	const static uint SAMPLE_COUNT = 512u;
	for (uint i = 0u; i < SAMPLE_COUNT; ++i)
	{
		float2 Xi = Hammersley(i, SAMPLE_COUNT);
		float3 H = ImportanceSampleGGX(Xi, N, roughness);
		float3 L = normalize(2.0 * dot(V, H) * H - V);

		float NdotL = max(L.z, 0.0);
		float NdotH = max(H.z, 0.0);
		float VdotH = max(dot(V, H), 0.0);

		if (NdotL > 0.0)
		{
			float Vis = Vis_SmithJointApprox(roughness * roughness, NdotV, NdotL );
			float G_Vis = NdotL * Vis * (4 * VdotH / NdotH);
			float Fc = pow(1.0 - VdotH, 5.0);

			A += (1.0 - Fc) * G_Vis;
			B += Fc * G_Vis;
		}
	}
	A /= float(SAMPLE_COUNT);
	B /= float(SAMPLE_COUNT);
	return float2(A, B);
}

RWTexture2D<float4> brdf : register(u0);
SamplerState s0 : register(s0);
cbuffer cb0 : register(b0) {
	int2 textureSize;
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	brdf[dtid.xy] = float4(IntegrateBRDF((dtid.x + 0.5) / textureSize.x, (dtid.y + 0.5) / textureSize.y), 0, 1);
}
struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.texcoord = float2((input.vertexId << 1) & 2, input.vertexId & 2);
	output.position = float4(output.texcoord.xy * 2.0 - 1.0, 0.0, 1.0);

	return output;
}
float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;
	return float4(IntegrateBRDF(uv.x, uv.y), 0, 1);
}
""", "csmain", "BRDFLUT.hlsl");
}
