#ifndef INPUT_SELF
Texture2D<float> input : register(t0);
#else
Texture2D<float2> input : register(t0);
#endif

RWTexture2D<float2> hiz : register(u0);
cbuffer cb0 : register(b0) {
	int2 inputSize;
}

bool validate(int2 position)
{
	return all(inputSize > position);
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	if (!validate((int2(dtid.xy))))
		return;
#ifndef INPUT_SELF
	float4 val;

	val.x = input[(dtid.xy * 2) + int2(0, 0)];

	if (validate((dtid.xy * 2) + int2(0, 1)))
		val.y = input[(dtid.xy * 2) + int2(0, 1)];
	else
		val.y = val.x;
	if (validate((dtid.xy * 2) + int2(0, 1)))
		val.z = input[(dtid.xy * 2) + int2(1, 0)];
	else
		val.z = val.x;
	if (validate((dtid.xy * 2) + int2(0, 1)))
		val.w = input[(dtid.xy * 2) + int2(1, 1)];
	else
		val.w = val.x;
	float min1 = min(min(val.x, val.y), min(val.z, val.w));
	float max1 = max(max(val.x, val.y), max(val.z, val.w));
	hiz[dtid.xy] = float2(min1, max1);
#else
	float4 val;
	float4 val1;

	float2 col[4];
	col[0].xy = input[(dtid.xy * 2) + int2(0, 0)].rg;

	for (int i = 1; i < 4; i++)
	{
		if (validate((dtid.xy * 2) + int2((i >> 1) & 1, i & 1)))
			col[i].rg = input[(dtid.xy * 2) + int2((i >> 1) & 1, i & 1)].rg;
		else
		{
			col[i].rg = col[0].rg;
		}
	}
	val.x = col[0].x;
	val.y = col[1].x;
	val.z = col[2].x;
	val.w = col[3].x;
	val1.x = col[0].y;
	val1.y = col[1].y;
	val1.z = col[2].y;
	val1.w = col[3].y;

	float min1 = min(min(val.x, val.y), min(val.z, val.w));
	float max1 = max(max(val1.x, val1.y), max(val1.z, val1.w));
	hiz[dtid.xy] = float2(min1, max1);
#endif
}