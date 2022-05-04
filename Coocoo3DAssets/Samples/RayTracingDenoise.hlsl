cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_camPos;
	float g_skyBoxMultiple;
	int2 _widthHeight;

};
Texture2D rayTracingResult :register(t0);
Texture2D gbuffer1 :register(t1);
Texture2D gbufferDepth : register (t2);

SamplerState s0 : register(s0);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);
float3 NormalDecode(float2 enc)
{
	float4 nn = float4(enc * 2, 0, 0) + float4(-1, -1, 1, -1);
	float l = dot(nn.xyz, -nn.xyw);
	nn.z = l;
	nn.xy *= sqrt(max(l, 1e-6));
	return nn.xyz * 2 + float3(0, 0, -1);
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

	float depth0 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float3 outputColor = float3(0, 0, 0);

	outputColor = rayTracingResult.SampleLevel(s3, uv, 0).rgb;
	return float4(outputColor, 0);
}