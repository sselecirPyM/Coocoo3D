#ifndef MAX_BONE_MATRICES
#define MAX_BONE_MATRICES 0
#endif
#define Skin4

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float4 Tan : TANGENT;		    //Normalized Tangent vector
};