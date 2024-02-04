using Coocoo3D.Components;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace RenderPipelines;

public class DrawDecalPass
{
    public Texture2D depthStencil;

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.PreserveAlpha,
        cullMode = CullMode.Front,
    };

    public Matrix4x4 viewProj;

    public IEnumerable<VisualComponent> Visuals;

    public void Execute(RenderHelper context)
    {
        BoundingFrustum frustum = new(viewProj);

        Span<byte> bufferData = stackalloc byte[64 + 64 + 16];

        foreach (var visual in Visuals)
        {
            if (visual.material.Type != Caprice.Display.UIShowType.Decal)
                continue;

            ref var transform = ref visual.transform;

            if (!frustum.Intersects(new BoundingSphere(transform.position, transform.scale.Length())))
                continue;

            var decalMaterial = DictExt.ConvertToObject<DecalMaterial>(visual.material.Parameters, context);

            DrawDecalFlag flag = DrawDecalFlag.None;

            if (decalMaterial.EnableDecalColor)
                flag |= DrawDecalFlag.ENABLE_DECAL_COLOR;
            if (decalMaterial.EnableDecalEmissive)
                flag |= DrawDecalFlag.ENABLE_DECAL_EMISSIVE;

            context.SetPSO(shader_drawDecal.Get(flag), psoDesc);


            Matrix4x4 m = transform.GetMatrix() * viewProj;
            Matrix4x4.Invert(m, out var im);
            MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(m));
            MemoryMarshal.Write(bufferData.Slice(64), Matrix4x4.Transpose(im));
            MemoryMarshal.Write(bufferData.Slice(128), decalMaterial._DecalEmissivePower);
            context.SetCBV<byte>(0, bufferData);

            context.SetSRV(0, depthStencil);
            context.SetSRV(1, decalMaterial.DecalColorTexture);
            context.SetSRV(2, decalMaterial.DecalEmissiveTexture);

            context.DrawCube();
        }
    }

    VariantShader<DrawDecalFlag> shader_drawDecal = new VariantShader<DrawDecalFlag>(
"""
cbuffer cb0 : register(b0)
{
	float4x4 g_mObjectToProj;
	float4x4 g_mProjToObject;
	float4 _DecalEmissivePower;
	//float _Metallic;
	//float _Roughness;
	//float _Emissive;
	//float _Specular;
	//float _AO;
}

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
Texture2D Depth :register(t0);
Texture2D Albedo :register(t1);
Texture2D Emissive :register(t2);

struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
	float4 texcoord1	: TEXCOORD1;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.position = float4((input.vertexId << 1) & 2, input.vertexId & 2, (input.vertexId >> 1) & 2, 1.0);
	output.position.xyz -= 1;

	output.position = mul(output.position, g_mObjectToProj);
	output.texcoord1 = output.position;
	output.texcoord = (output.position.xy / output.position.w) * 0.5f + 0.5f;

	return output;
}

struct MRTOutput
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
};

float4 albedoTexture(float2 uv)
{
	float width;
	float height;
	Albedo.GetDimensions(width, height);
	float2 XY = uv * float2(width, height);
	float2 alignmentXY = round(XY);
	float2 sampleUV = (alignmentXY + clamp((XY - alignmentXY) / fwidth(XY), -0.5f, 0.5f)) / float2(width, height);
	return Albedo.Sample(s1, sampleUV);
}

float4 emissiveTexture(float2 uv)
{
	float width;
	float height;
	Emissive.GetDimensions(width, height);
	float2 XY = uv * float2(width, height);
	float2 alignmentXY = round(XY);
	float2 sampleUV = (alignmentXY + clamp((XY - alignmentXY) / fwidth(XY), -0.5f, 0.5f)) / float2(width, height);
	return Emissive.Sample(s1, sampleUV);
}

MRTOutput psmain(PSIn input) : SV_TARGET
{
	MRTOutput output;
	output.color0 = float4(0, 0, 0, 0);
	output.color1 = float4(0, 0, 0, 0);

	float2 uv1 = input.texcoord1.xy / input.texcoord1.w;
	float2 uv = uv1 * 0.5 + 0.5;
	uv.y = 1 - uv.y;
	float depth = Depth.SampleLevel(s0, uv, 0);
	float4 objectPos = mul(float4(uv1, depth, 1), g_mProjToObject);
	objectPos /= objectPos.w;

	float2 objectUV = float2(objectPos.x * 0.5 + 0.5, 1 - (objectPos.y * 0.5 + 0.5));

	if (all(objectPos.xyz >= -1) && all(objectPos.xyz <= 1))
	{
#ifdef ENABLE_DECAL_COLOR
		output.color0 = albedoTexture(objectUV);
		//output.color0 = Albedo.Sample(s1, objectUV);
		output.color0.a *= smoothstep(0, 0.1, 1 - abs(objectPos.z));
#endif
#ifdef ENABLE_DECAL_EMISSIVE
		output.color1 = emissiveTexture(objectUV) * _DecalEmissivePower;
		//output.color1 = Emissive.Sample(s1, objectUV) * _DecalEmissivePower;
		output.color1.a *= smoothstep(0, 0.2, 1 - abs(objectPos.z));
#endif
		return output;
	}
	else
		clip(-0.1);

	return output;
}
""", "vsmain", null, "psmain");

	public void Dispose()
	{
		shader_drawDecal?.Dispose();
		shader_drawDecal = null;
	}

    [Flags]
    enum DrawDecalFlag
    {
        None = 0,
        ENABLE_DECAL_COLOR = 1,
        ENABLE_DECAL_EMISSIVE = 2
    }
}
