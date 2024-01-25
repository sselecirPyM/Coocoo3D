using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class BRDFBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
{
    static ushort[] quad = new ushort[] { 0, 1, 2, 2, 1, 3 };
    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
    {
        this.renderWrap = renderWrap;
        if (shader_BRDFLUT == null)
            Initialize();
        renderWrap.SetRenderTarget(texture, true);
        var psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
            rtvFormat = texture.GetFormat(),
            inputLayout = InputLayout.Default,
            renderTargetCount = 1,
        };
        renderWrap.SetPSO(shader_BRDFLUT, psoDesc);
        renderWrap.graphicsContext.SetMesh(null, MemoryMarshal.Cast<ushort, byte>(quad), 0, 6);
        renderWrap.Draw(6, 0, 0);
        return true;
    }
    RenderWrap renderWrap;

    void Initialize()
    {
        shader_BRDFLUT = RenderHelper.CreatePipeline(source_shader_BRDFLUT, "vsmain", null, "psmain", Path.Combine(this.renderWrap.BasePath, "BRDFLUT.hlsl"));
    }

    public void Dispose()
    {
        shader_BRDFLUT?.Dispose();
		shader_BRDFLUT = null;
    }

    PSO shader_BRDFLUT;

    string source_shader_BRDFLUT =
"""
//ref: https://learnopengl.com/PBR/IBL/Specular-IBL
#include "Random.hlsli"
#ifndef PI
#define PI 3.14159265358979
#endif
#pragma VertexShader vsmain
#pragma PixelShader psmain
float GeometrySchlickGGX(float NdotV, float roughness)
{
	float a = roughness;
	float k = (a * a) / 2.0;

	float nom = NdotV;
	float denom = NdotV * (1.0 - k) + k;

	return nom / denom;
}

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
	float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
	float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

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
			float G = GeometrySmith(N, V, L, roughness);
			float G_Vis = (G * VdotH) / (NdotH * NdotV);
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
	brdf[dtid.xy] = float4(IntegrateBRDF((dtid.x + 0.5) / textureSize.x, (dtid.y + 0.5) / textureSize.y), 0, 0);
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
""";
}
