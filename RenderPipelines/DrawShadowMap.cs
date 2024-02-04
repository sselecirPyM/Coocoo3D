using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class DrawShadowMap
{
    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
        depthBias = 2000,
        slopeScaledDepthBias = 1.5f,
    };

    public Matrix4x4 viewProjection = Matrix4x4.Identity;

    public void Execute(RenderHelper context)
    {
        var desc = psoDesc;
        var dsv = context.renderWrap.depthStencil;
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();

        Span<byte> bufferData = stackalloc byte[64];

        foreach (var renderable in context.MeshRenderables())
        {
            MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform * viewProjection));
            if (renderable.drawDoubleFace)
                desc.cullMode = CullMode.None;
            else
                desc.cullMode = CullMode.Back;
            context.SetPSO(shader_shadow, desc);
            context.SetCBV(0, bufferData);
            context.Draw(renderable);
        }
    }

    public VariantShader shader_shadow = new VariantShader(
"""
cbuffer cb1 : register(b0)
{
	float4x4 g_transform;
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
};

float4 vsmain(VSSkinnedIn input) : SV_POSITION
{
	return mul(float4(input.Pos, 1), g_transform);
}
""", "vsmain", null, null, "shadowMap.hlsl");

    public void Dispose()
    {
        shader_shadow?.Dispose();
        shader_shadow = null;
    }
}
