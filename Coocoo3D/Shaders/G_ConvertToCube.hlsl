cbuffer cb0 : register(b0)
{
	uint2 imageSize;
	int quality;
	uint batch;
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
const static float InvPIE = 1 / COO_PI;
float2 ComputeSphereCoord(float3 normal)
{
	normal = clamp(normal, -1.0, 1.0);
	float2 coord = float2((atan2(-normal.x, normal.z) * InvPIE * 0.5f + 0.5f), acos(normal.y) * InvPIE);
	return coord;
}
RWTexture2DArray<float4> CubeMap : register(u0);
Texture2D Panorama : register(t0);
SamplerState s0 : register(s0);

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float3(0, 0, 0);
	uint2 size1 = imageSize;
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)size1 * 2 - 1;
	if (dtid.x > size1.x || dtid.y > size1.y)
	{
		return;
	}
	if (dtid.z == 0)
	{
		N = mul(float4(screenPos, 0, 1), _xproj);
	}
	else if (dtid.z == 1)
	{
		N = mul(float4(screenPos, 0, 1), _nxproj);
	}
	else if (dtid.z == 2)
	{
		N = mul(float4(screenPos, 0, 1), _yproj);
	}
	else if (dtid.z == 3)
	{
		N = mul(float4(screenPos, 0, 1), _nyproj);
	}
	else if (dtid.z == 4)
	{
		N = mul(float4(screenPos, 0, 1), _zproj);
	}
	else
	{
		N = mul(float4(screenPos, 0, 1), _nzproj);
	}
	N = normalize(N);
	CubeMap[dtid] = float4(Panorama.SampleLevel(s0, ComputeSphereCoord(N), 0).rgb, 0);
}