
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