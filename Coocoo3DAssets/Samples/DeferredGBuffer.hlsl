#ifdef SKINNING
#define MAX_BONE_MATRICES 1024
#endif

#include "Skinning.hlsli"
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

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

	SkinnedInfo vSkinned = SkinVert(input, g_mConstBoneWorld);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
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
float2 NormalEncode(float3 n)
{
	n.xy /= dot(1, abs(n));
	if (n.z <= 0)
	{
		n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? float2(1, 1) : float2(-1, -1));
	}
	return n.xy;
}

struct MRTOutput
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

MRTOutput psmain(PSSkinnedIn input) : SV_TARGET
{
#if !USE_NORMAL_MAP
	float3 N = normalize(input.Norm);
#else
	float3x3 tbn = float3x3(normalize(input.Tangent), normalize(input.Bitangent), normalize(input.Norm));
	float3 dn = NormalMap.Sample(s1, input.Tex) * 2 - 1;
	float3 N = normalize(mul(dn, tbn));
#endif
	float2 encodedNormal = NormalEncode(N);
	MRTOutput output;
	float4 color = Albedo.Sample(s1, input.Tex);
	//clip(color.a - 0.98f);
	float4 metallic1 = Metallic.Sample(s1, input.Tex);
	float4 roughness1 = Roughness.Sample(s1, input.Tex);
	float roughness = max(_Roughness * roughness1.g, 0.002);

	float3 albedo = color.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic * metallic1.b);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic * metallic1.b);
	float3 emissive = Emissive.Sample(s1, input.Tex) * _Emissive;

#if USE_SPA
	float3 t1 = g_camLeft;
	float3 t2 = g_camDown;
	float2 spaUV = float2(dot(N, t1) * 0.5 + 0.5, dot(N, t2) * 0.5 + 0.5);
	emissive += Spa.SampleLevel(s0, spaUV, 0).rgb;
#endif
	output.color0 = float4(c_diffuse, c_specular.r);
	output.color1 = float4(encodedNormal, roughness, c_specular.g);
	output.color2 = float4(emissive, c_specular.b);
	output.color3 = float4(_AO, 0, 0, 1);
	return output;
}