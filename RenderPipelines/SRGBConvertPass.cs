using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;

namespace RenderPipelines;

public class SRGBConvertPass
{
    public Texture2D inputColor;
    public Texture2D inputColor1;

    public RenderHelper renderHelper;

    VariantShader shader_srgbConvert = new VariantShader(
"""
Texture2D inputColor : register(t0);
Texture2D inputColor1 : register(t1);

SamplerState s0 : register(s0);

struct VSIn
{
        uint vertexId : SV_VertexID;
};

struct PSIn
{
        float4 position : SV_POSITION;
        float2 texcoord : TEXCOORD;
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
        float4 color0 = inputColor.SampleLevel(s0, uv, 0);
        float4 color1 = color0 + inputColor1.SampleLevel(s0, uv, 0);
        float3 mixColor = pow(max(color1.rgb, 0.0001),1 / 2.2f);
        return float4(mixColor, color0.a);
}
""", "vsmain", null, "psmain");

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
        renderTargetCount = 1,
        dsvFormat = Vortice.DXGI.Format.Unknown,
    };

    public void Execute()
    {
        RenderWrap renderWrap = renderHelper.renderWrap;

        var desc = psoDesc;
        desc.rtvFormat = renderWrap.RenderTargets[0].GetFormat();

        renderWrap.SetPSO(shader_srgbConvert, desc);
        renderWrap.SetSRV(0, inputColor);
        renderWrap.SetSRV(1, inputColor1);

        renderHelper.DrawQuad();
    }

    public void Dispose()
    {
        shader_srgbConvert?.Dispose();
        shader_srgbConvert = null;
    }
}
