using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Numerics;

namespace RenderPipelines;

public class DrawSkyBoxPass
{
    public Texture2D skybox;

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
        dsvFormat = Vortice.DXGI.Format.Unknown,
        renderTargetCount = 1,
    };

    public Matrix4x4 InvertViewProjection;
    public Vector3 CameraPosition;
    public float SkyLightMultiple = 3;

    Vector4[] array = new Vector4[]
    {
        new Vector4(-1, -1, 0, 1),
        new Vector4(1, -1, 0, 1),
        new Vector4(-1, 1, 0, 1),
        new Vector4(1, 1, 0, 1),
    };

    public void Execute(RenderHelper renderHelper)
    {
        if (shader_skybox == null)
            Initialize();

        RenderWrap renderWrap = renderHelper.renderWrap;

        var desc = psoDesc;
        desc.rtvFormat = renderWrap.RenderTargets[0].GetFormat();
        renderWrap.SetPSO(shader_skybox, desc);
        renderWrap.SetSRV(0, skybox);

        Span<float> floats = stackalloc float[17];

        for (int i = 0; i < 4; i++)
        {
            Vector4 a = array[i];
            Vector4 b = Vector4.Transform(a, InvertViewProjection);
            b /= b.W;
            Vector3 dir = new Vector3(b.X, b.Y, b.Z) - CameraPosition;
            dir.CopyTo(floats[(i * 4)..]);
        }
        floats[16] = SkyLightMultiple;
        renderWrap.graphicsContext.SetCBVRSlot<float>(0, floats);

        renderHelper.DrawQuad();
    }

    public void Initialize()
    {
        shader_skybox = RenderHelper.CreatePipeline(source_shader_skybox, "vsmain", null, "psmain");
    }

    public void Dispose()
    {
        shader_skybox?.Dispose();
        shader_skybox = null;
    }

    readonly string source_shader_skybox =
"""
cbuffer cb0 : register(b0)
{
    float4 g_dir[4];
    float g_skyBoxMultiple;
};
TextureCube EnvCube : register(t0);
SamplerState s0 : register(s0);

struct VSIn
{
    uint vertexId : SV_VertexID;
};

struct PSIn
{
    float4 position : SV_POSITION;
    float4 direction : TEXCOORD;
};

PSIn vsmain(VSIn input)
{
    PSIn output;
    float2 position = float2((input.vertexId << 1) & 2, input.vertexId & 2) - 1.0;
    output.position = float4(position, 0.0, 1.0);
    output.direction = g_dir[clamp(input.vertexId, 0, 3)];
    return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
    float3 viewDir = input.direction;
    return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple, 1);
}
""";

    PSO shader_skybox;
}
