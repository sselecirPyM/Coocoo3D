cbuffer cbufferVertex
{
	float2 scale;
	float2 translate;
};

struct VS_INPUT
{
	float2 pos : POSITION;
	float2 uv  : TEXCOORD0;
	float4 col : COLOR0;
};

struct PS_INPUT
{
	float4 pos : SV_POSITION;
	float2 uv  : TEXCOORD0;
	float4 col : COLOR0;
};

PS_INPUT vsmain(VS_INPUT input)
{
	PS_INPUT output;
	output.pos = float4(mad(input.pos, scale, translate), 0, 1);
	output.col = input.col;
	output.uv = input.uv;
	return output;
}

sampler sampler0;
Texture2D texture0;

float4 psmain(PS_INPUT input) : SV_Target
{
	float4 output = input.col * texture0.Sample(sampler0, input.uv);
	return output;
}