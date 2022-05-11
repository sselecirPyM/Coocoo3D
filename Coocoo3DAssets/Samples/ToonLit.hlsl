//Copyright (c) 2020 ColinLeung-NiloCat
// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
// 
//Copyright (c) 2022 sselecirPyM

#ifdef SKINNING
#define MAX_BONE_MATRICES 1024
#endif

#include "Skinning.hlsli"
#include "PBR.hlsli"
#include "SH.hlsli"

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float3 LightColor;
	float useless;
};
struct PointLightInfo
{
	float3 LightPos;
	uint LightType;
	float3 LightColor;
	float LightRange;
};
cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
#if ENABLE_POINT_LIGHT
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
#endif
	float _Metallic;
	float _Roughness;
	float _Emissive;
	float _Specular;

	//emission
	//float _UseEmission;
	//float3 _EmissionColor;
	float _EmissionMulByBaseColor;
	//float4 _EmissionMapChannelMask;
	//lighting
	float3 _IndirectLightMinColor;
	//float _IndirectLightMultiplier;
	//float _DirectLightMultiplier;
	float _CelShadeMidPoint;
	float _CelShadeSoftness;
	//float _MainLightIgnoreCelShade;
	//float _AdditionalLightIgnoreCelShade;
	//shadow
	float _ReceiveShadowMappingAmount;
	//float _ReceiveShadowMappingPosOffset;
	float3 _ShadowMapColor;
	//Outline
	float _OutlineWidth;
	float3 _OutlineColor;
	//float _OutlineZOffset;
	//float _OutlineZOffsetMaskRemapStart;
	//float _OutlineZOffsetMaskRemapEnd;
}

struct ToonSurfaceData
{
	half3   albedo;
	half    alpha;
	half3   emission;
	half    occlusion;
};
struct ToonLightingData
{
	half3   normalWS;
	float3  positionWS;
	half3   viewDirectionWS;
	float4  shadowCoord;
};

cbuffer cb2 : register(b2)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mWorldToView;
	float3 g_camPos;
	float _Brightness;
	float g_cameraFar;
	float g_cameraNear;
	float g_cameraFov;
	float g_cameraAspectRatio;
	float4x4 LightMapVP;
	float4x4 LightMapVP1;
	LightInfo Lightings[1];
	float g_skyBoxMultiple;
	float3 _fogColor;
	float _fogDensity;
	float _startDistance;
	float _endDistance;
	int  g_lightMapSplit;
	float3 g_GIVolumePosition;
	float3 g_GIVolumeSize;
}

const static bool _IsFace = false;


ToonSurfaceData InitializeSurfaceData(float4 baseColor, float3 emissiveColor, float occulsion)
{
	ToonSurfaceData output;

	// albedo & alpha
	float4 baseColorFinal = baseColor;
	output.albedo = baseColorFinal.rgb;
	output.alpha = baseColorFinal.a;
	//DoClipTestToTargetAlphaValue(output.alpha);// early exit if possible

	// emission
	output.emission = emissiveColor;

	// occlusion
	output.occlusion = occulsion;

	return output;
}

ToonLightingData InitializeLightingData(float3 positionWS, float3 cameraPosition, float3 normal)
{
	ToonLightingData lightingData;
	lightingData.positionWS = positionWS;
	lightingData.viewDirectionWS = normalize(cameraPosition - lightingData.positionWS);
	lightingData.normalWS = normalize(normal); //interpolated normal is NOT unit vector, we need to normalize it

	return lightingData;
}

float GetOutlineCameraFovAndDistanceFixMultiplier(float positionVS_Z)
{
	float cameraMulFix;
	////////////////////////////////
	// Perspective camera case
	////////////////////////////////

	// keep outline similar width on screen accoss all camera distance       
	cameraMulFix = abs(positionVS_Z);

	// can replace to a tonemap function if a smooth stop is needed
	cameraMulFix = saturate(cameraMulFix);

	// keep outline similar width on screen accoss all camera fov
	cameraMulFix *= g_cameraFov * 180 / 3.14159265358979;

	return cameraMulFix * 0.00005; // mul a const to make return result = default normal expand amount WS
}

float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS)
{
	//you can replace it to your own method! Here we will write a simple world space method for tutorial reason, it is not the best method!
	float outlineExpandAmount = _OutlineWidth * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);
	return positionWS + normalWS * outlineExpandAmount;
}

half3 ShadeGI(ToonSurfaceData surfaceData, ToonLightingData lightingData, float3 averageSH)
{
	// hide 3D feeling by ignoring all detail SH (leaving only the constant SH term)
	// we just want some average envi indirect color only
	//half3 averageSH = SampleSH(0);

	// can prevent result becomes completely black if lightprobe was not baked 
	averageSH = max(_IndirectLightMinColor, averageSH);

	// occlusion (maximum 50% darken for indirect to prevent result becomes completely black)
	half indirectOcclusion = lerp(1, surfaceData.occlusion, 0.5);
	return averageSH * indirectOcclusion;
}

half3 ShadeSingleLight(ToonSurfaceData surfaceData, ToonLightingData lightingData, float3 lightColor, float3 direction, float distanceAttenuation, float shadowAttenuation, bool isAdditionalLight)
{
	half3 N = lightingData.normalWS;
	half3 L = direction;

	half NoL = dot(N, L);

	half lightAttenuation = 1;

	// light's distance & angle fade for point light & spot light (see GetAdditionalPerObjectLight(...) in Lighting.hlsl)
	distanceAttenuation = min(4, distanceAttenuation); //clamp to prevent light over bright if point/spot light too close to vertex

	// N dot L
	// simplest 1 line cel shade, you can always replace this line by your own method!
	half litOrShadowArea = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, NoL);

	// occlusion
	litOrShadowArea *= surfaceData.occlusion;

	// face ignore celshade since it is usually very ugly using NoL method
	litOrShadowArea = _IsFace ? lerp(0.5, 1, litOrShadowArea) : litOrShadowArea;

	// light's shadow map
	litOrShadowArea *= lerp(1, shadowAttenuation, _ReceiveShadowMappingAmount);

	half3 litOrShadowColor = lerp(pow(_ShadowMapColor, 2.2f), 1, litOrShadowArea);

	half3 lightAttenuationRGB = litOrShadowColor * distanceAttenuation;

	// saturate() light.color to prevent over bright
	// additional light reduce intensity since it is additive
	return saturate(lightColor) * lightAttenuationRGB * (isAdditionalLight ? 0.25 : 1);
}

half3 ShadeEmission(ToonSurfaceData surfaceData)
{
	half3 emissionResult = lerp(surfaceData.emission, surfaceData.emission * surfaceData.albedo, _EmissionMulByBaseColor); // optional mul albedo
	return emissionResult;
}

half3 CompositeAllLightResults(half3 indirectResult, half3 mainLightResult, half3 additionalLightSumResult, half3 emissionResult, ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
	// [remember you can write anything here, this is just a simple tutorial method]
	// here we prevent light over bright,
	// while still want to preserve light color's hue
	half3 rawLightSum = max(indirectResult, mainLightResult + additionalLightSumResult); // pick the highest between indirect and direct light
	return surfaceData.albedo * rawLightSum + emissionResult;
}

#define SH_RESOLUTION (16)
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);
Texture2D texture0 : register(t0);
Texture2D Metallic : register(t1);
Texture2D Roughness : register(t2);
Texture2D Emissive : register(t3);
Texture2D ShadowMap0 : register(t4);
TextureCube EnvCube : register (t5);
Texture2D BRDFLut : register(t6);
Texture2D NormalMap : register(t7);
StructuredBuffer<SH9C> giBuffer : register(t8);
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

float PointShadow(int index, float2 samplePos)
{
	float x = (float)(index % g_lightMapSplit) / (float)g_lightMapSplit;
	float y = (float)(index / g_lightMapSplit) / (float)g_lightMapSplit;
	float shadowDepth = ShadowMap0.SampleLevel(s3, samplePos / g_lightMapSplit + float2(x, y + 0.5), 0);
	return shadowDepth;
}

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float3 Bitangent : BITANGENT;
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;

	SkinnedInfo vSkinned = SkinVert(input, g_mConstBoneWorld);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.Bitangent = cross(output.Norm, output.Tangent) * input.Tan.w;
	output.Tex = input.Tex;
	float3 view = mul(vSkinned.Pos, g_mWorldToView);
#ifdef OUTLINE
	pos = TransformPositionWSToOutlinePositionWS(pos, view.z, output.Norm);
#endif

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

	return output;
}
#define ENABLE_EMISSIVE 1
#define ENABLE_DIFFUSE 1
#define ENABLE_SPECULR 1

#ifdef DEBUG_SPECULAR_RENDER
#undef ENABLE_DIFFUSE
#undef ENABLE_EMISSIVE
#endif

#ifdef DEBUG_DIFFUSE_RENDER
#undef ENABLE_SPECULR
#undef ENABLE_EMISSIVE
#endif

float getDepth(float z, float near, float far)
{
	return (far - (far / z) * near) / (far - near);
}

float4 psmain(PSSkinnedIn input) : SV_TARGET
{
	float4 texColor = texture0.Sample(s1, input.Tex);
	clip(texColor.a - 0.01f);

	float4 wPos = input.wPos;
	float3 cam2Surf = g_camPos - wPos;
	float camDist = length(cam2Surf);
	float3 V = normalize(cam2Surf);
#if !USE_NORMAL_MAP
	float3 N = normalize(input.Norm);
#else
	float3x3 tbn = float3x3(normalize(input.Tangent), normalize(input.Bitangent), normalize(input.Norm));
	float3 dn = NormalMap.Sample(s1, input.Tex) * 2 - 1;
	float3 N = normalize(mul(dn, tbn));
#endif
	float NdotV = saturate(dot(N, V));

	// Burley roughness bias
	float4 metallic1 = Metallic.Sample(s1, input.Tex);
	float4 roughness1 = Roughness.Sample(s1, input.Tex);
	float roughness = max(_Roughness * roughness1.g, 0.002);
	float alpha = roughness * roughness;

	float3 albedo = texColor.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic * metallic1.b);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic * metallic1.b);

	float3 outputColor = float3(0, 0, 0);
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	float3 emissive = Emissive.Sample(s1, input.Tex) * _Emissive;

	// fillin ToonSurfaceData struct:
	ToonSurfaceData surfaceData = InitializeSurfaceData(texColor, emissive, 1.0f);

	// fillin ToonLightingData struct:
	ToonLightingData lightingData = InitializeLightingData(wPos.xyz, g_camPos, N);
	float3 mainLightResult = float3(0, 0, 0);
	float3 additionalLightSumResult = float3(0, 0, 0);
	float3 emissionResult = ShadeEmission(surfaceData);

	float3 giResult = float3 (0, 0, 0);
	float3 skyLight = EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple;
#ifndef ENABLE_GI
	giResult = skyLight * c_diffuse;
#else
	float3 shDiffuse = GetSH(giBuffer, SH_RESOLUTION, g_GIVolumePosition, g_GIVolumeSize, N, wPos, skyLight);
	giResult = shDiffuse.rgb * c_diffuse;
#endif

#if ENABLE_SPECULR
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness,1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
#if ENABLE_EMISSIVE
	outputColor += emissive;
#endif

#if DEBUG_ALBEDO
	return float4(albedo, 1);
#endif
#if DEBUG_DEPTH
	float _depth1 = pow(input.Pos.z,2.2f);
	if (_depth1 < 1)
		return float4(_depth1, _depth1, _depth1,1);
	else
		return float4(1, 0, 0, 1);
#endif
#if DEBUG_DIFFUSE
	return float4(c_diffuse,1);
#endif
#if DEBUG_EMISSIVE
	return float4(emissive, 1);
#endif
#if DEBUG_NORMAL
	return float4(pow(N * 0.5 + 0.5, 2.2f), 1);
#endif
#if DEBUG_TANGENT
	return float4(pow(normalize(input.Tangent) * 0.5 + 0.5, 2.2f), 1);
#endif
#if DEBUG_BITANGENT
	return float4(pow(normalize(input.Bitangent) * 0.5 + 0.5, 2.2f), 1);
#endif
#if DEBUG_POSITION
	return wPos;
#endif
#if DEBUG_ROUGHNESS
	float _roughness1 = pow(max(roughness,0.0001f), 2.2f);
	return float4(_roughness1, _roughness1, _roughness1,1);
#endif
#if DEBUG_SPECULAR
	return float4(c_specular,1);
#endif
#if DEBUG_UV
	return float4(input.Tex,0,1);
#endif
#if ENABLE_DIRECTIONAL_LIGHT
	for (int i = 0; i < 1; i++)
	{
		if (!any(Lightings[i].LightColor))continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb, 0) / 3.14159265;
			if (i == 0)
			{
#ifndef DISBLE_SHADOW_RECEIVE
				float4 sPos;
				float2 shadowTexCoords;
				sPos = mul(wPos, LightMapVP);
				sPos = sPos / sPos.w;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5 ,0.5), sPos.z).r;
				else
				{
					sPos = mul(wPos, LightMapVP1);
					sPos = sPos / sPos.w;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
				}
#endif
			}
			float3 L = normalize(Lightings[i].LightDir);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			mainLightResult = ShadeSingleLight(surfaceData, lightingData, lightStrength, L, 1, inShadow, false);
		}
	}
#endif //ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_POINT_LIGHT
	int shadowmapIndex = 0;
	for (int i = 0; i < POINT_LIGHT_COUNT; i++, shadowmapIndex += 6)
	{
		float inShadow = 1.0f;
		float3 vl = PointLights[i].LightPos - wPos;
		float lightDistance2 = dot(vl, vl);
		float lightRange = PointLights[i].LightRange;
		if (pow2(lightRange) < lightDistance2)
			continue;
		float3 lightStrength = PointLights[i].LightColor.rgb;


		float3 L = normalize(vl);
		float3 H = normalize(L + V);

		float3 NdotL = saturate(dot(N, L));
		float3 LdotH = saturate(dot(L, H));
		float3 NdotH = saturate(dot(N, H));

		float3 absL = abs(L);


		if (absL.x > absL.y && absL.x > absL.z)
		{
			float2 samplePos = L.zy / L.x * 0.5 + 0.5;
			int mapindex = shadowmapIndex;
			if (L.x < 0)
				samplePos.x = 1 - samplePos.x;
			if (L.x > 0)
				mapindex++;

			float shadowDepth = PointShadow(mapindex, samplePos);
			inShadow = (shadowDepth) > getDepth(abs(vl.x), lightRange * 0.001f, lightRange) ? 1 : 0;
		}
		else if (absL.y > absL.z)
		{
			float2 samplePos = L.xz / L.y * 0.5 + 0.5;
			int mapindex = shadowmapIndex + 2;
			if (L.y < 0)
				samplePos.x = 1 - samplePos.x;
			if (L.y > 0)
				mapindex++;
			float shadowDepth = PointShadow(mapindex, samplePos);
			inShadow = (shadowDepth) > getDepth(abs(vl.y), lightRange * 0.001f, lightRange) ? 1 : 0;
		}
		else
		{
			float2 samplePos = L.yx / L.z * 0.5 + 0.5;
			int mapindex = shadowmapIndex + 4;
			if (L.z < 0)
				samplePos.x = 1 - samplePos.x;
			if (L.z > 0)
				mapindex++;
			float shadowDepth = PointShadow(mapindex, samplePos);
			inShadow = (shadowDepth) > getDepth(abs(vl.z), lightRange * 0.001f, lightRange) ? 1 : 0;
		}

		float lightStrength1 = sqrt(dot(lightStrength, lightStrength) / 3);
		additionalLightSumResult += ShadeSingleLight(surfaceData, lightingData, lightStrength / lightStrength1, L, lightStrength1 / lightDistance2, inShadow, true);
	}
#endif //ENABLE_POINT_LIGHT
	outputColor = CompositeAllLightResults(giResult, mainLightResult, additionalLightSumResult, emissionResult, surfaceData, lightingData);

#ifdef OUTLINE
	outputColor = outputColor * _OutlineColor;
#endif

#if ENABLE_FOG
	outputColor = lerp(pow(max(_fogColor , 1e-6),2.2f), outputColor,1 / exp(max((camDist - _startDistance) / 10,0.00001) * _fogDensity));
#endif

	return float4(outputColor * _Brightness, texColor.a);
}