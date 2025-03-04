using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class PureColorBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker, IDisposable
{
    static ushort[] quad = new ushort[] { 0, 1, 2, 2, 1, 3 };
    public bool Bake(Texture2D texture, RenderPipelineView view, ref object tag)
    {

        view.SetRenderTarget(texture, true);
        var psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
            rtvFormat = texture.GetFormat(),
            renderTargetCount = 1,
        };

        Span<float> cbvData = stackalloc float[4];
        Color.CopyTo(cbvData);
        view.graphicsContext.SetCBVRSlot<float>(0, cbvData);

        view.SetPSO(shader_pureColor, psoDesc);
        view.graphicsContext.SetSimpleMesh(null, MemoryMarshal.AsBytes<ushort>(quad), 0, 2);
        view.Draw(6, 0, 0);
        return true;
    }

    public Vector4 Color { get; }

    public PureColorBakerAttribute(float r, float g, float b, float a)
    {
        Color = new Vector4(r, g, b, a);
    }

    public void Dispose()
    {
        shader_pureColor?.Dispose();
        shader_pureColor = null;
    }

    VariantShader shader_pureColor = new VariantShader(
"""
RWTexture2D<float4> tex : register(u0);
SamplerState s0 : register(s0);
cbuffer cb0 : register(b0) {
	float4 color;
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	tex[dtid.xy] = color;
}
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
	output.position = float4(output.texcoord.xy * 2.0 - 1.0, 0.0, 1.0);

	return output;
}
float4 psmain(PSIn input) : SV_TARGET
{
	return color;
}
""", "vsmain", null, "psmain");
}
