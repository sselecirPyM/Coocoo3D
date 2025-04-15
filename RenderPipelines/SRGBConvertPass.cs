using Coocoo3DGraphics;
using RenderPipelines.Utility;

namespace RenderPipelines
{
    public class SRGBConvertPass
    {
        public void Execute()
        {
            var graphicsContext = context.renderPipelineView.graphicsContext;
            context.SetPSO(shader_srgb_convert, new PSODesc() { cullMode = CullMode.None });
            graphicsContext.SetGraphicsResources((ct) =>
            {
                ct.SetSRV(0, inputColor);
                ct.SetSRV(1, inputColor1);
            });
            context.DrawQuad2();
        }

        public void Dispose()
        {
            shader_srgb_convert?.Dispose();
        }
        VariantShader shader_srgb_convert = new VariantShader("""
Texture2D inputColor : register(t0);
Texture2D inputColor1 : register(t1);

SamplerState s0 : register(s0);

struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.texcoord = float2((input.vertexId << 1) & 2, input.vertexId & 2);
	output.position = float4(output.texcoord.xy * 2.0 - 1.0, 0.999, 1.0);

	return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;
	float4 origin = inputColor.SampleLevel(s0, uv, 0);
	float4 color1 = origin + inputColor1.SampleLevel(s0, uv, 0);
	color1.rgb = pow(max(color1.rgb, 0.0001),1 / 2.2f);
	return float4(color1.rgb, origin.a);
}
			
""", "vsmain", null, "psmain");
        public RenderHelper context;
        public Texture2D inputColor;
        public Texture2D inputColor1;
    }
}
