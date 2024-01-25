using Coocoo3DGraphics;
using System;

namespace RenderPipelines;

public class GenerateMipPass : IDisposable
{
    public Texture2D input;

    public Texture2D output;


    public void Execute(RenderHelper renderHelper)
    {
        if (shader_generateMipMap == null)
            Initialize();
        var renderWrap = renderHelper.renderWrap;

        int width = input.width;
        int height = input.height;

        renderWrap.SetSRV(0, input);
        renderWrap.SetUAV(0, output, 0);
        renderWrap.graphicsContext.SetCBVRSlot<int>(0, [width, height]);

        renderWrap.SetPSO(shader_generateMipMap);
        renderWrap.Dispatch((width + 15) / 16, (height + 15) / 16);

        int x = width;
        int y = height;
        for (int i = 1; i < 9; i++)
        {
            x = (x + 1) / 2;
            y = (y + 1) / 2;
            renderWrap.graphicsContext.SetCBVRSlot<int>(0, [x, y]);
            renderWrap.SetSRVLim(0, output, i - 1);
            renderWrap.SetUAV(0, output, i);

            renderWrap.Dispatch((x + 15) / 16, (y + 15) / 16);
        }
    }

    void Initialize()
    {
        shader_generateMipMap = RenderHelper.CreateComputeShader(source_shader_generateMipMap, "csmain");
    }

    public void Dispose()
    {
        shader_generateMipMap?.Dispose();
        shader_generateMipMap = null;
    }

    ComputeShader shader_generateMipMap;
    readonly string source_shader_generateMipMap =
"""
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
""";
}
