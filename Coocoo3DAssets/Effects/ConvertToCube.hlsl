cbuffer cb0 : register(b0)
{
    uint2 imageSize;
}

const static float MATH_PI = 3.141592653589793238;
const static float InvPI = 1 / MATH_PI;
float2 ComputeSphereCoord(float3 normal)
{
    normal = clamp(normal, -1.0, 1.0);
    float2 coord = float2((atan2(-normal.x, normal.z) * InvPI * 0.5f + 0.5f), acos(normal.y) * InvPI);
    return coord;
}
RWTexture2DArray<float4> CubeMap : register(u0);
Texture2D Panorama : register(t0);
SamplerState s0 : register(s0);

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
    float3 N = float3(0, 0, 0);
    float2 screenPos = ((float2) dtid.xy + 0.5f) / (float2) imageSize * 2 - 1;
    if (dtid.x > imageSize.x || dtid.y > imageSize.y)
    {
        return;
    }
    if (dtid.z == 0)
    {
        N = float3(1, -screenPos.y, -screenPos.x);
    }
    else if (dtid.z == 1)
    {
        N = float3(-1, -screenPos.y, screenPos.x);
    }
    else if (dtid.z == 2)
    {
        N = float3(screenPos.x, 1, screenPos.y);
    }
    else if (dtid.z == 3)
    {
        N = float3(screenPos.x, -1, -screenPos.y);
    }
    else if (dtid.z == 4)
    {
        N = float3(screenPos.x, -screenPos.y, 1);
    }
    else
    {
        N = float3(-screenPos.x, -screenPos.y, -1);
    }
    N = normalize(N);
    CubeMap[dtid] = float4(Panorama.SampleLevel(s0, ComputeSphereCoord(N), 0).rgb, 0);
}