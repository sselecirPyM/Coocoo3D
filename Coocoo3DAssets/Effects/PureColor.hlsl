#pragma VertexShader vsmain
#pragma PixelShader psmain
RWTexture2D<float4> tex : register(u0);
SamplerState s0 : register(s0);
cbuffer cb0 : register(b0) {
	float4 color;
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	tex[dtid.xy] = color;
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
	return color;
}