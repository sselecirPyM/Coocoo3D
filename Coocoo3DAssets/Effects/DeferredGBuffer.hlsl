#include "Skinning.hlsli"
#include "GBufferDefine.hlsli"
#pragma VertexShader vsmain
#pragma PixelShader psmain

cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
	float4x4 g_mWorldToProj;
	float _Metallic;
	float _Roughness;
	float _Emissive;
	float _Specular;
	float _AO;
	float3 g_camLeft;
	float3 g_camDown;
}

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float3 Bitangent : BITANGENT;
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;
	
    float3 pos = mul(float4(input.Pos, 1), g_mWorld);
    output.Norm = normalize(mul(input.Norm, (float3x3) g_mWorld));
    output.Tangent = normalize(mul(input.Tan, (float3x3) g_mWorld));
	output.Bitangent = cross(output.Norm, output.Tangent) * input.Tan.w;
	output.Tex = input.Tex;

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

	return output;
}

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);

Texture2D Albedo :register(t0);
Texture2D Metallic :register(t1);
Texture2D Roughness :register(t2);
Texture2D Emissive :register(t3);
Texture2D NormalMap :register(t4);
Texture2D Spa :register(t5);

float4 albedoTexture(float2 uv)
{
	float width;
	float height;
	Albedo.GetDimensions(width, height);
	float2 XY = uv * float2(width, height);
	float2 alignmentXY = round(XY);
	float2 sampleUV = (alignmentXY + clamp((XY - alignmentXY) / fwidth(XY), -0.5f, 0.5f)) / float2(width, height);
	return Albedo.Sample(s1, sampleUV);
}

float4 emissiveTexture(float2 uv)
{
	float width;
	float height;
	Emissive.GetDimensions(width, height);
	float2 XY = uv * float2(width, height);
	float2 alignmentXY = round(XY);
	float2 sampleUV = (alignmentXY + clamp((XY - alignmentXY) / fwidth(XY), -0.5f, 0.5f)) / float2(width, height);
	return Emissive.Sample(s1, sampleUV);
}

GBufferData psmain(PSSkinnedIn input) : SV_TARGET
{
#if !USE_NORMAL_MAP
	float3 N = normalize(input.Norm);
#else
	float3x3 tbn = float3x3(normalize(input.Tangent), normalize(input.Bitangent), normalize(input.Norm));
	float3 dn = NormalMap.Sample(s1, input.Tex) * 2 - 1;
	float3 N = normalize(mul(dn, tbn));
#endif
	//float4 color = Albedo.Sample(s1, input.Tex);
	float4 color = albedoTexture(input.Tex);
	//clip(color.a - 0.98f);
	float4 metallic1 = Metallic.Sample(s1, input.Tex);
	float4 roughness1 = Roughness.Sample(s1, input.Tex);
	float roughness = max(_Roughness * roughness1.g, 0.002);

	float3 albedo = color.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic * metallic1.b);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic * metallic1.b);
	//float3 emissive = Emissive.Sample(s1, input.Tex) * _Emissive;
	float3 emissive = emissiveTexture(input.Tex) * _Emissive;

#if USE_SPA
	float3 t1 = g_camLeft;
	float3 t2 = g_camDown;
	float2 spaUV = float2(dot(N, t1) * 0.5 + 0.5, dot(N, t2) * 0.5 + 0.5);
	emissive += Spa.SampleLevel(s0, spaUV, 0).rgb;
#endif
	GBufferData output;
	GBufferDefault(output);
	GBufferDiffuseSpecular(output, c_diffuse, c_specular);
	GBufferRoughness(output, roughness);
	GBufferEmissive(output, emissive);
	GBufferAO(output, _AO);
	GBufferNormal(output, N);
	return output;
}