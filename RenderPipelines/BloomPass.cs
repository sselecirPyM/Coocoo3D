using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Numerics;

namespace RenderPipelines;

public class BloomPass
{
    public Texture2D intermediaTexture;

    public Texture2D input;

    public Texture2D output;

    public int mipLevel;

    public (int, int) inputSize;


    public float BloomThreshold = 1.05f;
    public float BloomIntensity = 0.1f;

    public VariantComputeShader<Keyword_Bloom> shader_bloom = new VariantComputeShader<Keyword_Bloom>(
"""
cbuffer cb0 : register(b0)
{
	float2 _offset;
	float2 _coord;
	float threshold;
	float intensity;
}
Texture2D texture0 : register(t0);
RWTexture2D<float4> outputTexture : register(u0);
SamplerState s0 : register(s0);

//const static uint countOfWeights = 16;
//const static float weights[16] = {
//0.07994048,
//0.078357555,
//0.073794365,
//0.0667719,
//0.058048703,
//0.048486352,
//0.03891121,
//0.03000255,
//0.022226434,
//0.015820118,
//0.010818767,
//0.0071084364,
//0.0044874395,
//0.0027217707,
//0.0015861068,
//0.0008880585,
//};

const static uint countOfWeights = 31;
const static float weights[31] = {
0.0465605101786725,
0.0462447158367745,
0.0453101259577882,
0.0437942600982133,
0.0417568651253835,
0.0392760089004505,
0.0364431226197036,
0.0333574300811589,
0.0301202350068439,
0.0268295196499428,
0.0235752446327683,
0.0204356421098791,
0.0174746762382393,
0.0147407220589254,
0.0122664006719526,
0.0100694165014753,
0.00815417885334333,
0.0065139576487105,
0.00513332070564347,
0.00399062238619951,
0.00306035382567913,
0.0023152154746489,
0.00172782582903609,
0.00127202977405506,
0.000923811493801257,
0.00066184793066898,
0.000467758662380463,
0.000326117595851012,
0.000224292832094226,
0.000152175706578895,
0.00010185070247391,
};

groupshared float4 sampledColor[64 + 2 * 32];
#ifdef BLOOM_1
[numthreads(64, 1, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID, uint groupIndex : SV_GroupIndex)
{
	float2 offset = _offset;
	float2 coords = (dtid.xy + 0.5) * _coord;
	float4 color = float4(0, 0, 0, 0);

	sampledColor[groupIndex * 2 + 0] = max(texture0.SampleLevel(s0, coords + ((int)groupIndex + 0 - 32) * offset, 0) - threshold, 0);
	sampledColor[groupIndex * 2 + 1] = max(texture0.SampleLevel(s0, coords + ((int)groupIndex + 1 - 32) * offset, 0) - threshold, 0);

	GroupMemoryBarrierWithGroupSync();

	for (int i = countOfWeights - 1; i > 0; i--)
	{
		color += sampledColor[32 - i + groupIndex] * weights[i];
	}
	for (int i = 0; i < countOfWeights; i++)
	{
		color += sampledColor[32 + i + groupIndex] * weights[i];
	}
	outputTexture[dtid.xy] = float4(color.rgb, 1);
}
#endif
#ifdef BLOOM_2
[numthreads(1, 64, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID, uint groupIndex : SV_GroupIndex)
{
	float2 offset = _offset;
	float2 coords = (dtid.xy + 0.5) * _coord;
	float4 color = float4(0, 0, 0, 0);

	sampledColor[groupIndex * 2 + 0] = texture0.SampleLevel(s0, coords + ((int)groupIndex + 0 - 32) * offset, 0);
	sampledColor[groupIndex * 2 + 1] = texture0.SampleLevel(s0, coords + ((int)groupIndex + 1 - 32) * offset, 0);

	GroupMemoryBarrierWithGroupSync();

	for (int i = countOfWeights - 1; i > 0; i--)
	{
		color += sampledColor[32 - i + groupIndex] * weights[i];
	}
	for (int i = 0; i < countOfWeights; i++)
	{
		color += sampledColor[32 + i + groupIndex] * weights[i];
	}
	outputTexture[dtid.xy] = float4(color.rgb * intensity, 1);
}
#endif

//#ifdef BLOOM_1
//[numthreads(64, 1, 1)]
//void csmain(uint3 dtid : SV_DispatchThreadID)
//{
//	float2 offset = _offset;
//	float2 coords = dtid.xy * _coord;
//	float4 color = float4(0, 0, 0, 0);
//	for (int i = countOfWeights - 1; i > 0; i--)
//	{
//		color += max(texture0.SampleLevel(s0, coords - i * offset, 0) - threshold, 0) * weights[i];
//	}
//	for (int i = 0; i < countOfWeights; i++)
//	{
//		color += max(texture0.SampleLevel(s0, coords + i * offset, 0) - threshold, 0) * weights[i];
//	}
//	color.a = 1;
//	outputTexture[dtid.xy] = color;
//}
//#endif
//#ifdef BLOOM_2
//[numthreads(1, 64, 1)]
//void csmain(uint3 dtid : SV_DispatchThreadID)
//{
//	float2 offset = _offset;
//	float2 coords = (dtid.xy + 0.5) * _coord;
//
//	float4 color = float4(0, 0, 0, 0);
//	for (int i = countOfWeights - 1; i > 0; i--)
//	{
//		color += texture0.SampleLevel(s0, coords - i * offset, 0) * weights[i];
//	}
//	for (int i = 0; i < countOfWeights; i++)
//	{
//		color += texture0.SampleLevel(s0, coords + i * offset, 0) * weights[i];
//	}
//	color.a = 1;
//	outputTexture[dtid.xy] = color * intensity;
//}
//#endif
""", "csmain");

    public void Execute(RenderHelper renderHelper)
    {
        var renderWrap = renderHelper.renderWrap;

        Span<float> cbv1 = stackalloc float[6];

        cbv1[4] = BloomThreshold;
        cbv1[5] = BloomIntensity;
        Vector2 intermediaSize = new Vector2(intermediaTexture.width, intermediaTexture.height);
        float x = (float)inputSize.Item1 / input.width / intermediaSize.X;
        cbv1[0] = x;
        cbv1[1] = 0;
        cbv1[2] = x;
        cbv1[3] = (float)inputSize.Item2 / input.height / intermediaSize.Y;
        renderWrap.graphicsContext.SetCBVRSlot<float>(0, cbv1);


        renderWrap.SetSRVMip(0, input, mipLevel);
        renderWrap.SetUAV(0, intermediaTexture);
        renderWrap.SetPSO(shader_bloom.Get(Keyword_Bloom.BLOOM_1));
        renderWrap.Dispatch((intermediaTexture.width + 63) / 64, intermediaTexture.height);

        cbv1[0] = 0;
        cbv1[1] = 1.0f / intermediaSize.Y;
        cbv1[2] = 1.0f / (float)output.width;
        cbv1[3] = 1.0f / (float)output.height;
        renderWrap.graphicsContext.SetCBVRSlot<float>(0, cbv1);


        renderWrap.SetSRV(0, intermediaTexture);
        renderWrap.SetUAV(0, output);
        renderWrap.SetPSO(shader_bloom.Get(Keyword_Bloom.BLOOM_2));
        renderWrap.Dispatch(output.width, (output.height + 63) / 64);
    }
}

public enum Keyword_Bloom
{
    None = 0,
    BLOOM_1 = 1,
    BLOOM_2 = 2,
}
