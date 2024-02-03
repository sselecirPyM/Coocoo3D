using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class DrawSkyBoxPass
{
    public Texture2D skybox;

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.None,
        cullMode = CullMode.None,
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

    public void Execute(RenderHelper context)
    {
        context.SetPSO(shader_skybox, psoDesc);
        context.SetSRV(0, skybox);

        Span<float> vertexs = stackalloc float[16];
        Span<ushort> indices = [0, 1, 2, 2, 1, 3];

        for (int i = 0; i < 4; i++)
        {
            Vector4 b = Vector4.Transform(array[i], InvertViewProjection);
            b /= b.W;
            Vector3 dir = new Vector3(b.X, b.Y, b.Z) - CameraPosition;
            dir.CopyTo(vertexs[(i * 4)..]);
        }
        context.SetCBV<float>(0, [SkyLightMultiple]);

        context.SetSimpleMesh(MemoryMarshal.AsBytes(vertexs), MemoryMarshal.AsBytes(indices), 16, 2);
        context.DrawIndexedInstanced(6, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        shader_skybox?.Dispose();
        shader_skybox = null;
    }

    VariantShader shader_skybox = new VariantShader(
"""
cbuffer cb0 : register(b0)
{
    //float4 g_dir[4];
    float g_skyBoxMultiple;
};
TextureCube EnvCube : register(t0);
SamplerState s0 : register(s0);

struct VSIn
{
    uint vertexId : SV_VertexID;
    float4 direction : TEXCOORD;
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
    //output.direction = g_dir[clamp(input.vertexId, 0, 3)];
    output.direction = input.direction;
    return output;
}

float4 psmain(PSIn input) : SV_TARGET
{
    float3 viewDir = input.direction;
    return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple, 1);
}
""", "vsmain", null, "psmain");
}
