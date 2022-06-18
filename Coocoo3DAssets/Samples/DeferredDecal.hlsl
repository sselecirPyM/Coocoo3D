
cbuffer cb0 : register(b0)
{
	float4x4 g_mObjectToProj;
	float4x4 g_mProjToObject;
	float4 _DecalEmissivePower;
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

struct MRTOutput
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
};

MRTOutput psmain(PSIn input) : SV_TARGET
{
	MRTOutput output;
	output.color0 = float4(0, 0, 0, 0);
	output.color1 = float4(0, 0, 0, 0);

	float2 uv1 = input.texcoord1.xy / input.texcoord1.w;
	float2 uv = uv1 * 0.5 + 0.5;
	uv.y = 1 - uv.y;
	float depth = Depth.SampleLevel(s0, uv, 0);
	float4 objectPos = mul(float4(uv1, depth, 1), g_mProjToObject);
	objectPos /= objectPos.w;

	float2 objectUV = float2(objectPos.x * 0.5 + 0.5, 1 - (objectPos.y * 0.5 + 0.5));

	if (all(objectPos.xyz >= -1) && all(objectPos.xyz <= 1))
	{
#ifdef ENABLE_DECAL_COLOR
		output.color0 = Albedo.Sample(s1, objectUV);
		output.color0.a *= smoothstep(0, 0.1, 1 - abs(objectPos.z));
#endif
#ifdef ENABLE_DECAL_EMISSIVE
		output.color1 = Emissive.Sample(s1, objectUV) * _DecalEmissivePower;
		output.color1.a *= smoothstep(0, 0.2, 1 - abs(objectPos.z));
#endif
		return output;
	}
	else
		clip(-0.1);

	return output;
	//return float4(uv, 0, 0);
}