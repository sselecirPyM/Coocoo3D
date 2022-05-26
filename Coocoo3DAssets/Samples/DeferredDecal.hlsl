
cbuffer cb0 : register(b0)
{
	float4x4 g_mObjectToProj;
	float4x4 g_mProjToObject;
	//float _Metallic;
	//float _Roughness;
	//float _Emissive;
	//float _Specular;
	//float _AO;
}

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
Texture2D Depth :register(t0);
Texture2D Albedo :register(t1);
Texture2D Emissive :register(t2);

struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
	float4 texcoord1	: TEXCOORD1;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.position = float4((input.vertexId << 1) & 2, input.vertexId & 2, (input.vertexId >> 1) & 2, 1.0);
	output.position.xyz -= 1;

	output.position = mul(output.position, g_mObjectToProj);
	output.texcoord1 = output.position;
	output.texcoord = (output.position.xy / output.position.w) * 0.5f + 0.5f;

	return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
	//float2 uv = input.texcoord;
	//float2 uv = input.position.xy/ input.position.w * 0.5 + 0.5;

	float2 uv1 = input.texcoord1.xy / input.texcoord1.w;
	float2 uv = uv1 * 0.5 + 0.5;
	uv.y = 1 - uv.y;
	float depth = Depth.SampleLevel(s0, uv, 0);
	float4 objectPos = mul(float4(uv1, depth, 1), g_mProjToObject);
	objectPos /= objectPos.w;

	float2 objectUV = float2(objectPos.x * 0.5 + 0.5, 1 - (objectPos.y * 0.5 + 0.5));

	if (all(objectPos.xyz >= -1) && all(objectPos.xyz <= 1))
		return Albedo.Sample(s1, objectUV);
	else
		clip(-0.1);

	return float4(objectPos.xyz, 0);
	//return float4(uv, 0, 0);
}