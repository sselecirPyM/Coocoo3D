
cbuffer cb0 : register(b0)
{
	uint2 imageSize;
}
RWTexture2DArray<float4> IrradianceMap : register(u0);
[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	IrradianceMap[dtid] = float4(0, 0, 0, 1);
}