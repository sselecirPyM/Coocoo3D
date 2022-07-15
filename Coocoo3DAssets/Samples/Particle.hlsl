struct Particle
{
	float3 position;
	float scale;
};

cbuffer cb0:register(b0)
{
	float4x4 g_mWorld;
	float4x4 g_mViewProj;
	float4 particleColor;
	float g_cameraFar;
	float g_cameraNear;
	float3 left;
	float3 up;
	Particle particles[1024];
}

float getLinearDepth(float z)
{
	float far = g_cameraFar;
	float near = g_cameraNear;
	return near * far / (far + near - z * (far - near));
}

struct VSIn
{
	uint vertexId : SV_VertexID;
	uint instanceId : SV_InstanceID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
	float4 texcoord1: TEXCOORD1;
};

Texture2D Texture : register (t0);
Texture2D Depth : register (t1);
SamplerState s0 : register(s0);

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.texcoord = float2((input.vertexId << 1) & 2, input.vertexId & 2) * 0.5;

	float3 position = mul(float4(particles[input.instanceId].position, 1), g_mWorld);
	float scale = particles[input.instanceId].scale;
	position += (output.texcoord.x * 2.0 - 1.0) * -left * scale;
	position += (output.texcoord.y * 2.0 - 1.0) * -up * scale;

	output.position = mul(float4(position, 1.0), g_mViewProj);
	output.texcoord1 = output.position;

	return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;

	float2 uv1 = (input.texcoord1.xy / input.texcoord1.w) * 0.5 + 0.5;
	uv1.y = 1 - uv1.y;
	float depth = getLinearDepth(input.position.z);
	float depth1 = getLinearDepth( Depth.SampleLevel(s0, uv1, 0));

	float4 color1 = Texture.SampleLevel(s0, uv, 0);
	color1 *= particleColor;
	color1.a *= 1 - smoothstep(depth1 - 0.1, depth1, depth);

	return color1;
}