
#ifndef MAX_BONE_MATRICES
#define MAX_BONE_MATRICES 0
#endif
#define Skin4

struct SkinnedInfo
{
	float4 Pos;
	float3 Norm;
	float3 Tan;
};

#if MAX_BONE_MATRICES > 0
struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
	float4 Weights : WEIGHTS;		//Bone weights
	uint4  Bones : BONES;			//Bone indices
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float4 Tan : TANGENT;		    //Normalized Tangent vector
};

SkinnedInfo SkinVert(VSSkinnedIn Input, float4x4 transforms[MAX_BONE_MATRICES])
{
	SkinnedInfo Output = (SkinnedInfo)0;

	float4 Pos = float4(Input.Pos, 1);
	float3 Norm = Input.Norm;
	float3 Tan = Input.Tan;

	for (int i = 0; i < 4; i++)
	{
		uint iBone = iBone = Input.Bones[i];
		float fWeight = fWeight = Input.Weights[i];
		if (iBone < MAX_BONE_MATRICES)
		{
			matrix m = transforms[iBone];
			Output.Pos += fWeight * mul(Pos, m);
			Output.Norm += fWeight * mul(float4(Norm, 0), m).xyz;
			Output.Tan += fWeight * mul(float4(Tan, 0), m).xyz;
		}
	}
	return Output;
}
#else
struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float4 Tan : TANGENT;		    //Normalized Tangent vector
};

SkinnedInfo SkinVert(VSSkinnedIn Input, float4x4 transforms[0])
{
	SkinnedInfo Output = (SkinnedInfo)0;
	Output.Pos = float4(Input.Pos, 1);
	Output.Norm = Input.Norm;
	Output.Tan = Input.Tan;
	return Output;
}
#endif