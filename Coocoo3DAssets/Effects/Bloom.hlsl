cbuffer cb0 : register(b0)
{
	float2 _offset;
	float threshold;
	float intensity;
	float2 _coord;
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

//const static uint countOfWeights = 64;
//const static float weights[64] = {
//0.022172735902703404,
//0.0221385451062412,
//0.022036288729079572,
//0.02186690994492944,
//0.021631964604755465,
//0.021333597442362334,
//0.020974509478731283,
//0.020557917317902824,
//0.020087505192154347,
//0.01956737075596201,
//0.019001965743232218,
//0.018396032687878466,
//0.01775453896229099,
//0.017082609410869875,
//0.01638545884681973,
//0.015668325641039316,
//0.014936407564263687,
//0.014194800950493804,
//0.013448444134700688,
//0.012702065984855841,
//0.011960140201915468,
//0.011226845906070806,
//0.01050603486800691,
//0.009801205584608053,
//0.009115484243771347,
//0.008451612476620407,
//0.007811941660839308,
//0.007198433418897726,
//0.0066126658518121054,
//0.006055844964334608,
//0.0055288206719747495,
//0.005032106734295617,
//0.004565903932148702,
//0.0041301257980432245,
//0.0037244262173470647,
//0.0033482282417651205,
//0.0030007534935229765,
//0.002681051586683714,
//0.002388029048717297,
//0.0021204772884735375,
//0.0018770992237814258,
//0.0016565342508310572,
//0.0014573813062876127,
//0.0012782198399612663,
//0.0011176285792872415,
//0.000974202025606422,
//0.0008465646753099304,
//0.0007333830056322304,
//0.0006333753048243023,
//0.000545319459437206,
//0.0004680588375440469,
//0.0004005064261637977,
//0.00034164739432114886,
//0.0002905402606048411,
//0.000246316846386493,
//0.0002081811937056373,
//0.00017540762091735614,
//0.00014733808024270954,
//0.00012337897004638458,
//0.00010299754164025574,
//8.57180262734728E-05,
//7.111759325446943E-05,
//5.8822235323844274E-05,
//4.850266285297847E-05,
//};

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