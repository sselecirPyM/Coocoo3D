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

cbuffer cb0 : register(b0)
{
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_skyBoxMultiple;
	float _Brightness;
};
TextureCube EnvCube : register (t0);
SamplerState s0 : register(s0);

float4 psmain(PSIn input) : SV_TARGET
{
	float4 vx = mul(float4(input.texcoord * 2 - 1,0,1),g_mProjToWorld);
	float3 viewDir = vx.xyz / vx.w - g_vCamPos;
	return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple * _Brightness, 1);
}