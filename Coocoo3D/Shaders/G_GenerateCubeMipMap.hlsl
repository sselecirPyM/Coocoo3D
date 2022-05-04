cbuffer cb0 : register(b0)
{
	uint2 imageSize;
	int mips;
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
[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float3(0, 0, 0);
	float4 dir1 = float4(0, 0, 0, 0);
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)imageSize * 2 - 1;
	if (dtid.x > imageSize.x || dtid.y > imageSize.y)
	{
		return;
	}
	if (dtid.z == 0)
	{
		dir1 = mul(float4(screenPos, 0, 1), _xproj);
	}
	else if (dtid.z == 1)
	{
		dir1 = mul(float4(screenPos, 0, 1), _nxproj);
	}
	else if (dtid.z == 2)
	{
		dir1 = mul(float4(screenPos, 0, 1), _yproj);
	}
	else if (dtid.z == 3)
	{
		dir1 = mul(float4(screenPos, 0, 1), _nyproj);
	}
	else if (dtid.z == 4)
	{
		dir1 = mul(float4(screenPos, 0, 1), _zproj);
	}
	else
	{
		dir1 = mul(float4(screenPos, 0, 1), _nzproj);
	}
	N = normalize(dir1.xyz / dir1.w);
	IrradianceMap[dtid] =Image.SampleLevel(s0, N, mips);
}