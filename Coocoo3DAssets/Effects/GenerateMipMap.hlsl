cbuffer cb0 : register(b0)
{
	uint2 imageSize;
}

RWTexture2D<float4> Target : register(u0);
Texture2D Image : register(t0);
SamplerState s0 : register(s0);
[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	if (dtid.x < (imageSize.x + 1) / 2 && dtid.y < (imageSize.y + 1) / 2)
	{
		int2 sourcePosition = dtid.xy << 1;
		float4 colors[4];
		colors[0] = Image[sourcePosition];
		[unroll]
		for (int i = 1; i < 4; i++)
		{
			int2 pos = sourcePosition + int2((i >> 1) & 1, i & 1);
			if (pos.x < imageSize.x && pos.y < imageSize.y)
				colors[i] = Image[pos];
			else
				colors[i] = colors[0];
		}

		Target[dtid.xy] = (colors[0] + colors[1] + colors[2] + colors[3]) * 0.25;
	}
}