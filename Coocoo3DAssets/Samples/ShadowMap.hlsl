#ifdef SKINNING
#define MAX_BONE_MATRICES 1024
#endif
#include "Skinning.hlsli"
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

cbuffer cb2 : register(b1)
{
	float4x4 g_mWorld;
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;

	SkinnedInfo vSkinned = SkinVert(input, g_mConstBoneWorld);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.Tex = input.Tex;

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

	return output;
}