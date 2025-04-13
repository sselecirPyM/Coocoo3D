using Coocoo3DGraphics;
using RenderPipelines.Utility;

namespace RenderPipelines
{
    public partial class GenerateMipPass
    {
        public void Execute()
        {
            context.SetPSO(shader_generateMip);
            var graphicsContext = context.renderPipelineView.graphicsContext;

            int x = input.width;
            int y = input.height;
            graphicsContext.SetComputeResources(s =>
            {
                s.SetCBV(0, [x, y]);
                s.SetSRV(0, input);
                s.SetUAV(0, output);
            });

            graphicsContext.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
            for (int i = 1; i < 9; i++)
            {
                x = (x + 1) / 2;
                y = (y + 1) / 2;

                graphicsContext.SetComputeResources(s =>
                {
                    s.SetCBV(0, [x, y]);
                    s.SetSRVMip(0, output, i - 1);
                    s.SetUAVMip(0, output, i);
                });

                graphicsContext.Dispatch((x + 15) / 16, (y + 15) / 16, 1);
            }
        }


        public Texture2D input;
        public Texture2D output;

        public void Dispose()
        {
            shader_generateMip?.Dispose();
        }
        VariantComputeShader shader_generateMip = new VariantComputeShader("""

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
			
""", "csmain", null);
        public RenderHelper context;
    }
}
