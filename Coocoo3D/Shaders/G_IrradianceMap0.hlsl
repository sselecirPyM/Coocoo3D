// Ref: http://www.reedbeta.com/blog/quick-and-easy-gpu-random-numbers-in-d3d11/
namespace RNG
{
	static const float RANDOM_NUMBER_PI = 3.141592653589793238;
	uint RandomSeed(uint seed)
	{
		// Thomas Wang hash 
		// Ref: http://www.burtleburtle.net/bob/hash/integer.html
		seed = (seed ^ 61) ^ (seed >> 16);
		seed *= 9;
		seed = seed ^ (seed >> 4);
		seed *= 0x27d4eb2d;
		seed = seed ^ (seed >> 15);
		return seed;
	}

	// Generate a random 32-bit integer
	uint Random(inout uint state)
	{
		// Xorshift algorithm from George Marsaglia's paper.
		state ^= (state << 13);
		state ^= (state >> 17);
		state ^= (state << 5);
		return state;
	}

	// Generate a random float in the range [0.0f, 1.0f)
	float Random01(inout uint state)
	{
		return asfloat(0x3f800000 | Random(state) >> 9) - 1.0;
	}

	// Generate a random float in the range [0.0f, 1.0f]
	float Random01inclusive(inout uint state)
	{
		return Random(state) / float(0xffffffff);
	}

	// Generate a random integer in the range [lower, upper]
	uint Random(inout uint state, uint lower, uint upper)
	{
		return lower + uint(float(upper - lower + 1) * Random01(state));
	}

	//Generate normal distribution random float ~N(0,1)
	float NDRandom(inout uint state)
	{
		float R = sqrt(-2 * log(1 - Random01(state)));
		float theta = 2 * 3.141592653589793238 * Random01(state);
		return R * cos(theta);
	}

	float2 Hammersley(uint Index, uint NumSamples, uint2 Random)
	{
		float E1 = frac((float)Index / NumSamples + float(Random.x & 0xffff) / (1 << 16));
		float E2 = float(reversebits(Index) ^ Random.y) * 2.3283064365386963e-10;
		return float2(E1, E2);
	}

	float3 HemisphereSampleCos(float2 Xi)
	{
		float phi = 2 * RANDOM_NUMBER_PI * Xi.x;

		float cosTheta = sqrt(Xi.y);
		float sinTheta = sqrt(1 - cosTheta * cosTheta);

		float3 H;
		H.x = sinTheta * cos(phi);
		H.y = sinTheta * sin(phi);
		H.z = cosTheta;

		return H;
	}

	float3 UniformSampleHemisphere(float2 Xi)
	{
		float phi = 2 * RNG::RANDOM_NUMBER_PI * Xi.x;

		float cosTheta = Xi.y;
		float sinTheta = sqrt(1 - cosTheta * cosTheta);

		float3 H;
		H.x = sinTheta * cos(phi);
		H.y = sinTheta * sin(phi);
		H.z = cosTheta;

		return H;
	}
}
cbuffer cb0 : register(b0)
{
	uint2 imageSize;
	int quality;
	uint batch;
	int notUse;
	int face;
}


const static float4x4 _xproj =
{ 0,0,-1,0,
0,-1,0,0,
0,0,0,-100,
1,0,0,100, };
const static float4x4 _nxproj =
{ 0,0,1,0,
0,-1,0,0,
0,0,0,-100,
-1,0,0,100, };
const static float4x4 _yproj =
{ 1,0,0,0,
0,0,1,0,
0,0,0,-100,
0,1,0,100, };
const static float4x4 _nyproj =
{ 1,0,0,0,
0,0,-1,0,
0,0,0,-100,
0,-1,0,100, };
const static float4x4 _zproj =
{ 1,0,0,0,
0,-1,0,0,
0,0,0,-100,
0,0,1,100, };
const static float4x4 _nzproj =
{ -1,0,0,0,
0,-1,0,0,
0,0,0,-100,
0,0,-1,100, };
const static float COO_PI = 3.141592653589793238;

float4 Pow4(float4 x)
{
	return x * x * x * x;
}
float3 Pow4(float3 x)
{
	return x * x * x * x;
}
float2 Pow4(float2 x)
{
	return x * x * x * x;
}
float Pow4(float x)
{
	return x * x * x * x;
}
float4 Pow2(float4 x)
{
	return x * x;
}
float3 Pow2(float3 x)
{
	return x * x;
}
float2 Pow2(float2 x)
{
	return x * x;
}
float Pow2(float x)
{
	return x * x;
}

float3x3 GetTangentBasis(float3 TangentZ)
{
	const float Sign = TangentZ.z >= 0 ? 1 : -1;
	const float a = -rcp(Sign + TangentZ.z);
	const float b = TangentZ.x * TangentZ.y * a;

	float3 TangentX = { 1 + Sign * a * Pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * Pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

RWTexture2DArray<float4> IrradianceMap : register(u0);
TextureCube Image : register(t0);
SamplerState s0 : register(s0);
[numthreads(4, 4, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float3(0, 0, 0);
	float4 dir1 = float4(0, 0, 0, 0);
	//int __face = dtid.z;
	int __face = face;
	uint randomState = RNG::RandomSeed(dtid.x + dtid.y * 2048 + __face * 4194304 + batch * 67108864);
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)imageSize * 2 - 1;
	if (dtid.x > imageSize.x || dtid.y > imageSize.y)
	{
		return;
	}
	if (__face == 0)
	{
		dir1 = mul(float4(screenPos, 0, 1), _xproj);
	}
	else if (__face == 1)
	{
		dir1 = mul(float4(screenPos, 0, 1), _nxproj);
	}
	else if (__face == 2)
	{
		dir1 = mul(float4(screenPos, 0, 1), _yproj);
	}
	else if (__face == 3)
	{
		dir1 = mul(float4(screenPos, 0, 1), _nyproj);
	}
	else if (__face == 4)
	{
		dir1 = mul(float4(screenPos, 0, 1), _zproj);
	}
	else
	{
		dir1 = mul(float4(screenPos, 0, 1), _nzproj);
	}
	N = normalize(dir1.xyz / dir1.w);
	float3 col1 = float3(0, 0, 0);
	const int c_sampleCount = 256;
	for (int i = 0; i < c_sampleCount; i++)
	{
		float2 E = RNG::Hammersley(i, c_sampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
		float3 vec1 = TangentToWorld(N, RNG::HemisphereSampleCos(E));

		float NdotL = dot(vec1, N);
		col1 += Image.SampleLevel(s0, vec1, 2) * NdotL;
	}
	float xd0 = 1 / (float)(quality + 1);
	float xd1 = quality / (float)(quality + 1);
	IrradianceMap[uint3(dtid.xy, __face)] = float4(col1 / c_sampleCount / 3.14159265359f * xd0 + IrradianceMap[uint3(dtid.xy, __face)].rgb * xd1, 1);
}