struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
};

Texture2D noPostProcess : register (t0);
SamplerState s0 : register(s0);

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.texcoord = float2((input.vertexId << 1) & 2, input.vertexId & 2);
	output.position = float4(output.texcoord.xy * 2.0 - 1.0, 0.999, 1.0);

	return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;

	float4 color1 = noPostProcess.SampleLevel(s0, uv, 0);
	color1.rgb = pow(max(color1.rgb, 0.0001),1 / 2.2f);
	return float4(color1.rgb, 1);
}