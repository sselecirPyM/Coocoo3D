#include "PBR.hlsli"
#include "SH.hlsli"
#include "Random.hlsli"
#include "GBufferDefine.hlsli"

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

#define SH_RESOLUTION (16)
#pragma VertexShader vsmain
#pragma PixelShader psmain
float Pow2(float x)
{
	return x * x;
}

float Pow4(float x)
{
	return x * x * x * x;
}

float3x3 GetTangentBasis(float3 TangentZ)
{
	const float Sign = TangentZ.z >= 0 ? 1 : -1;
	const float a = -rcp(Sign + TangentZ.z);
	const float b = TangentZ.x * TangentZ.y * a;

	float3 TangentX = { 1 + Sign * a * Pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * Pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

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
cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float g_cameraFarClip;
	float g_cameraNearClip;
	float g_cameraFOV;
	float g_cameraAspectRatio;
	float3 g_camPos;
	float g_skyBoxMultiple;
	float3 _fogColor;
	float _fogDensity;
	float _startDistance;
	float _endDistance;
	int2 _widthHeight;
	float _Brightness;
	int _volumeLightIterCount;
	float _volumeLightMaxDistance;
	float _volumeLightIntensity;
	float4x4 LightMapVP;
	float4x4 LightMapVP1;
	LightInfo Lightings[1];
	float3 g_GIVolumePosition;
	float g_AODistance;
	float3 g_GIVolumeSize;
	float g_AOLimit;
	int g_AORaySampleCount;
	int g_RandomI;
	int g_lightMapSplit;
};

Texture2D gbuffer0 :register(t0);
Texture2D gbuffer1 :register(t1);
Texture2D gbuffer2 :register(t2);
Texture2D gbuffer3 :register(t3);
TextureCube EnvCube : register (t4);
Texture2D gbufferDepth : register (t5);
Texture2D ShadowMap : register(t6);
TextureCube SkyBox : register (t7);
Texture2D BRDFLut : register(t8);
Texture2D HiZ : register(t9);
StructuredBuffer<SH9C> giBuffer : register(t10);
StructuredBuffer<PointLightInfo> PointLights : register(t11);

SamplerState s0 : register(s0);
SamplerComparisonState sampleShadowMap : register(s2);
SamplerState s3 : register(s3);

float3 NormalDecode(float2 enc)
{
	float3 n = float3(enc, 1 - dot(1, abs(enc)));
	if (n.z < 0)
	{
		n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? float2(1, 1) : float2(-1, -1));
	}
	return normalize(n);
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

struct SurfaceInfo
{
	float3 diffuse;
	float3 specular;
	float roughness;
	float alpha;
	float3 normal;
};

float3 shadeLight(in SurfaceInfo surface, float LdotH, float NdotH, float NdotL, float NdotV)
{
#if ENABLE_DIFFUSE
	float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, surface.roughness);
#else
	float diffuse_factor = 0;
#endif
#if ENABLE_SPECULR
	float3 specular_factor = Specular_BRDF(surface.alpha, surface.specular, NdotV, NdotL, LdotH, NdotH);
#else
	float3 specular_factor = 0;
#endif

	return ((surface.diffuse * diffuse_factor / COO_PI) + specular_factor);
}

float getLinearDepth(float z)
{
	float far = g_cameraFarClip;
	float near = g_cameraNearClip;
	return near * far / (far + near - z * (far - near));
}

float getDepth(float z, float near, float far)
{
	return (far - (far / z) * near) / (far - near);
}

#if ENABLE_DIRECTIONAL_LIGHT
bool pointInLightRange(int index, float3 position)
{
	float4 sPos;
	float4 pos1 = float4(position, 1);
	sPos = mul(pos1, LightMapVP);
	sPos = sPos / sPos.w;
	if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
		return true;
	sPos = mul(pos1, LightMapVP1);
	sPos = sPos / sPos.w;
	if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
		return true;
	return false;
}

float4 shadowCmp(float2 uv, float z)
{
	return ShadowMap.SampleCmpLevelZero(sampleShadowMap, uv, z);
	//float width;
	//float height;
	//ShadowMap.GetDimensions(width, height);
	//float2 XY = uv * float2(width, height);
	//float2 alignmentXY = round(XY);
	//float2 sampleUV = (alignmentXY + clamp((XY - alignmentXY) / fwidth(XY), -0.5f, 0.5f)) / float2(width, height);
	//return ShadowMap.SampleCmpLevelZero(sampleShadowMap, sampleUV, z);
}

float pointInLight(int index, float3 position)
{
	float inShadow = 1;
	float4 sPos;
	float2 shadowTexCoords;
	float4 pos1 = float4(position, 1);
	sPos = mul(pos1, LightMapVP);
	sPos = sPos / sPos.w;
	shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
	shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
	if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
	{
		//inShadow = ShadowMap.SampleCmpLevelZero(sampleShadowMap, shadowTexCoords * float2(0.5, 0.5), sPos.z).r;
		inShadow = shadowCmp(shadowTexCoords * float2(0.5, 0.5), sPos.z).r;
	}
	else
	{
		sPos = mul(pos1, LightMapVP1);
		sPos = sPos / sPos.w;
		shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
		shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
		if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
			//inShadow = ShadowMap.SampleCmpLevelZero(sampleShadowMap, shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
			inShadow = shadowCmp(shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
	}
	return inShadow;
}
#endif

float PointShadow(int index, float2 samplePos)
{
	float x = (float)(index % g_lightMapSplit) / (float)g_lightMapSplit;
	float y = (float)(index / g_lightMapSplit) / (float)g_lightMapSplit;
	float shadowDepth = ShadowMap.SampleLevel(s3, samplePos / g_lightMapSplit + float2(x, y + 0.5), 0);
	return shadowDepth;
}
float3 Shade1(in SurfaceInfo surface, float3 wPos, float3 V, float3 emissive, float AO, bool SSRReflect)
{
	float3 N = surface.normal;
	float NdotV = saturate(dot(N, V));

	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, surface.roughness), 0).rg;
	float3 GF = surface.specular * AB.x + AB.y;

	float3 outputColor = float3(0, 0, 0);

#if ENABLE_DIFFUSE
	float3 skyLight = EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple;
#ifndef ENABLE_GI
	outputColor += skyLight * surface.diffuse * AO;
#else
	float3 shDiffuse = GetSH(giBuffer, SH_RESOLUTION, g_GIVolumePosition, g_GIVolumeSize, N, wPos, skyLight);
	outputColor += shDiffuse.rgb * surface.diffuse * AO;
#endif
#endif
#if ENABLE_SPECULR & !RAY_TRACING

#if !ENABLE_SSR
	SSRReflect = false;
#endif
	if (SSRReflect)
	{

	}
	else
	{
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(surface.roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF * AO;
	}
#endif

#ifdef ENABLE_EMISSIVE
	outputColor += emissive;
#endif

#if ENABLE_DIRECTIONAL_LIGHT
	for (int i = 0; i < 1; i++)
	{
		if (!any(Lightings[i].LightColor))continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
			float3 L = normalize(Lightings[i].LightDir);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			inShadow = pointInLight(0, wPos);

			outputColor += NdotL * lightStrength * inShadow * shadeLight(surface, LdotH, NdotH, NdotL, NdotV);
		}
	}
#endif//ENABLE_DIRECTIONAL_LIGHT
#if POINT_LIGHT_COUNT
	int shadowmapIndex = 0;
	for (int i = 0; i < POINT_LIGHT_COUNT; i++, shadowmapIndex += 6)
	{
		float inShadow = 1.0f;

		float3 vl = PointLights[i].LightPos - wPos;
		float lightDistance2 = dot(vl, vl);
		float lightRange = PointLights[i].LightRange;
		if (pow2(lightRange) < lightDistance2)
			continue;
		float3 lightStrength = PointLights[i].LightColor.rgb / lightDistance2;

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

		outputColor += NdotL * lightStrength * inShadow * shadeLight(surface, LdotH, NdotH, NdotL, NdotV);
	}
#endif//ENABLE_POINT_LIGHT

	return outputColor;
}

const static int c_hizSize = 4096;

int2 GetHiZStartPosition(int level)
{
	return int2(c_hizSize - (c_hizSize >> (level - 1)), 0);
}

bool DepthHit(float a, float b, float depth)
{
	return ((a >= depth && b <= depth + 0.0005) || (b >= depth && a <= depth + 0.0005)) && depth < 0.9999;
}

bool DepthHit1(float a, float b, float depthMin, float depthMax)
{
	return ((a >= depthMin && b <= depthMax + 0.0005) || (b >= depthMin && a <= depthMax + 0.0005)) && depthMin < 0.9999;
}

float3 Shade(in SurfaceInfo surface, float3 wPos, float3 V, float3 emissive, float AO, float2 uv)
{
	float3 N = surface.normal;
	float3 outputColor = float3(0, 0, 0);
#if ENABLE_SPECULR & ENABLE_SSR & !RAY_TRACING
	float NdotV = saturate(dot(N, V));

	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, surface.roughness), 0).rg;
	float3 GF = surface.specular * AB.x + AB.y;

	bool traced = false;
	bool hasClosestHit = false;
	//uint randomState = RNG::RandomSeed((int)(_widthHeight.x * uv.x) + (int)(_widthHeight.y * uv.y * 2048) + g_RandomI);
	//float2 E = Hammersley(0, 1, uint2(RNG::Random(randomState), RNG::Random(randomState)));
	//float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(roughness)).xyz, N);
	//float3 L = 2 * dot(V, H) * H - V;

	float3 reflectDir = -reflect(V, N);
	float4 rayStart = float4(wPos, 1);
	float4 rayEnd = float4(wPos + reflectDir, 1);
	float4 d1 = mul(rayEnd, g_mWorldToProj);
	float4 d2 = mul(rayStart, g_mWorldToProj);
	if (d1.z < 0)
	{
		d1 = d2 - (d2 - d1) / (d2.z - d1.z) * d2.z;
	}

	float4 a1 = float4(d1.xyz / d1.w, 1 / d1.w);
	float4 a2 = float4(d2.xyz / d2.w, 1 / d2.w);


	float4 sDir = a1 - a2;
	sDir.y = -sDir.y;
	sDir.xy *= _widthHeight / 2.0;
	float4 ppx = sDir / abs(sDir.x);
	float4 ppy = sDir / abs(sDir.y);

	float2 invDir = 1 / sDir.xy;

	float4 ppm;
	if (abs(sDir.x) > abs(sDir.y))
		ppm = ppx;
	else
		ppm = ppy;
	int2 tzi = int2(ppm.x > 0 ? 0 : 1, ppm.y > 0 ? 0 : 1);

	float4 fnext = float4(0, 0, 0, 0);
	fnext.zw = a2.zw;

	float closestHitDepth = 0;
	int2 closestHitTex = int2(0, 0);
	float inaccuracy = 0.002;

	int2 startPosition = uv * _widthHeight;

	bool enterOnce = false;
	bool previousHit = false;

	int currentBoardLevel = 1;

	for (int i = 0; i < 200; i++)
	{
		int2 next = int2(floor(fnext.xy));
		int2 currentPosition = startPosition + next;

		if (currentBoardLevel > 0)
		{
			int j = currentBoardLevel;
			int2 hizPosition = (currentPosition >> j);
			int2 nextZPosition = hizPosition + int2(sign(ppm.xy));
			int2 nextBPosition = (nextZPosition.xy << j) + tzi * ((1 << j) - 1);

			float2 tx = (nextBPosition - currentPosition);
			float2 tx1 = tx * invDir;
			float2 colHiZ = HiZ.mips[j - 1][hizPosition];
			float4 xNext = fnext;
			if (abs(tx1.x) > abs(tx1.y))
			{
				xNext += abs(tx.y) * ppy;
			}
			else
			{
				xNext += abs(tx.x) * ppx;
			}
			int2 hizPosition1 = (startPosition + int2(floor(xNext.xy)) >> j);
			if (hizPosition.x == hizPosition1.x && hizPosition.y == hizPosition1.y)
			{
				currentBoardLevel--;
				continue;
			}

			if (!DepthHit1(fnext.z, xNext.z, colHiZ.x, colHiZ.y))
			{
				fnext = xNext;
				currentBoardLevel++;
			}
			else
			{
				currentBoardLevel--;
			}
		}
		else
		{
			currentBoardLevel++;
		}
		currentBoardLevel = clamp(currentBoardLevel, 0, 9);


		float rayDepth = fnext.z;
		if (any((currentPosition) < 0) || any((currentPosition) >= _widthHeight) || rayDepth >= 1 || rayDepth <= 0)
			break;

		if (currentBoardLevel > 0)
			continue;

		float prevDepth = fnext.z;
		fnext += ppm;

		float cDepth = gbufferDepth[currentPosition].r;

		if (rayDepth > cDepth && previousHit)
			enterOnce = true;
		if (DepthHit(rayDepth, prevDepth, cDepth))
		{
			float4 buffer1Color = gbuffer1[currentPosition];
			float3 N1 = normalize(NormalDecode(buffer1Color.rg));
			if (dot(N1, reflectDir) > -0.1)
			{
				prevDepth = rayDepth;
				previousHit = false;
				continue;
			}
			previousHit = true;
			float inaccuracy1 = min(abs(rayDepth - cDepth), abs(prevDepth - cDepth));
			if (enterOnce)
				inaccuracy1 *= 0.25;
			if (inaccuracy1 < inaccuracy)
			{
				closestHitTex = currentPosition;
				closestHitDepth = cDepth;
				inaccuracy = inaccuracy1;
			}

			float c1 = (1 - dot(N1, V)) * 0.01;

			hasClosestHit = true;
			if ((rayDepth >= cDepth && prevDepth <= cDepth + c1) || (prevDepth >= cDepth && rayDepth <= cDepth + c1))
			{
				closestHitTex = currentPosition;
				closestHitDepth = cDepth;
				inaccuracy *= 0.1;
				enterOnce = true;
				break;
			}
		}
		else
		{
			previousHit = false;
		}
	}

	float3 envReflectColor = EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(surface.roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF * AO;
	if (hasClosestHit)
	{
		GBufferData gbufferData;
		gbufferData.color0 = gbuffer0[closestHitTex];
		gbufferData.color1 = gbuffer1[closestHitTex];
		gbufferData.color2 = gbuffer2[closestHitTex];
		gbufferData.color3 = gbuffer3[closestHitTex];

		float4 wPos1 = mul(float4((float)closestHitTex.x / _widthHeight.x * 2 - 1, 1 - (float)closestHitTex.y / _widthHeight.y * 2, closestHitDepth, 1), g_mProjToWorld);
		wPos1 /= wPos1.w;

		float3 N1 = GBufferGetNormal(gbufferData);

		float roughness1 = GBufferGetRoughness(gbufferData);
		float3 diffuse1 = GBufferDiffuse(gbufferData);
		float3 specular1 = GBufferSpecular(gbufferData);

		float3 emissive1 = GBufferGetEmissive(gbufferData);

		SurfaceInfo surface1;
		surface1.diffuse = diffuse1;
		surface1.specular = specular1;
		surface1.roughness = roughness1;
		surface1.alpha = roughness1 * roughness1;
		surface1.normal = N1;

		float3 reflectColor = Shade1(surface1, wPos1.xyz, -reflectDir, emissive1, 1, false) * GF * AO;
		if (!enterOnce)
			inaccuracy *= 16 * sqrt(closestHitDepth);

		float t = smoothstep(0.001, 0.002, inaccuracy);

		outputColor += lerp(reflectColor, envReflectColor, t);
	}
	else
		outputColor += envReflectColor;
#endif
	return Shade1(surface, wPos, V, emissive, AO, true) + outputColor;
}

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;

	GBufferData gbufferData;
	gbufferData.color0 = gbuffer0.SampleLevel(s3, uv, 0);
	gbufferData.color1 = gbuffer1.SampleLevel(s3, uv, 0);
	gbufferData.color2 = gbuffer2.SampleLevel(s3, uv, 0);
	gbufferData.color3 = gbuffer3.SampleLevel(s3, uv, 0);
	float depth1 = gbufferDepth.SampleLevel(s3, uv, 0).r;


	float4 wPos = mul(float4(input.texcoord * 2 - 1, depth1, 1), g_mProjToWorld);
	wPos /= wPos.w;

	float3 V = normalize(g_camPos - wPos);

	float3 cam2Surf = g_camPos - wPos;
	float camDist = length(cam2Surf);

	float3 outputColor = float3(0, 0, 0);
	if (depth1 != 1.0)
	{
		float roughness = GBufferGetRoughness(gbufferData);
		float3 diffuse = GBufferDiffuse(gbufferData);
		float3 specular = GBufferSpecular(gbufferData);
		float3 emissive = GBufferGetEmissive(gbufferData);
		float3 N = GBufferGetNormal(gbufferData);
		int2 sx = uv * _widthHeight;
		float AO = 1;
		float AOFactor = GBufferGetAO(gbufferData);
		SurfaceInfo surface;
		surface.diffuse = diffuse;
		surface.specular = specular;
		surface.roughness = roughness;
        surface.alpha = roughness * roughness;
        surface.normal = N;
   //     if (dot(N, V) > 0)
			//surface.normal = N;
   //     else
   //         surface.normal = -N;
#if ENABLE_SSAO
		uint randomState = RNG::RandomSeed(sx.x + sx.y * 2048 + g_RandomI);
		if (AOFactor != 0)
			for (int i = 0; i < g_AORaySampleCount; i++)
			{
				float2 E = Hammersley(i, g_AORaySampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
				float3 vec1 = TangentToWorld(N, UniformSampleHemisphere(E));
				const int sampleCountPerRay = 8;
				for (int j = 0; j < sampleCountPerRay; j++)
				{
					float4 samplePos = wPos + float4(vec1, 0) * (j + 0.5) / sampleCountPerRay * g_AODistance;
					float4 d1 = mul(samplePos, g_mWorldToProj);
					float3 _a1 = d1.xyz / d1.w;
					float2 _uv;
					_uv.x = _a1.x * 0.5 + 0.5;
					_uv.y = 0.5 - _a1.y * 0.5;
					float aoDepth = getLinearDepth(gbufferDepth.SampleLevel(s3, _uv, 0).r);
					float lz = getLinearDepth(_a1.z);
					float factor = (1 - j / (float)sampleCountPerRay) * AOFactor;
					if (lz > aoDepth + 0.01 && lz < aoDepth + g_AOLimit)
					{
						AO -= 1.0f / g_AORaySampleCount * factor;
						break;
					}
				}
			}
#endif


		outputColor += Shade(surface, wPos.xyz, V, emissive, AO, uv);

#if ENABLE_FOG
		outputColor = lerp(pow(max(_fogColor, 1e-6), 2.2f), outputColor, 1 / exp(max((camDist - _startDistance) / 10, 0.00001) * _fogDensity));
#endif

#if DEBUG_AO
		return float4(AO, AO, AO, 1);
#endif
#if DEBUG_DEPTH
		float _depth1 = pow(depth1, 2.2f);
		if (_depth1 < 1)
			return float4(_depth1, _depth1, _depth1, 1);
		else
			return float4(1, 0, 0, 1);
#endif
#if DEBUG_DIFFUSE
		return float4(diffuse, 1);
#endif
#if DEBUG_EMISSIVE
		return float4(emissive, 1);
#endif
#if DEBUG_NORMAL
		return float4(pow(N * 0.5 + 0.5, 2.2f), 1);
#endif
#if DEBUG_POSITION
		return wPos;
#endif
#if DEBUG_ROUGHNESS
		float _roughness1 = pow(max(roughness, 0.0001f), 2.2f);
		return float4(_roughness1, _roughness1, _roughness1, 1);
#endif
#if DEBUG_SPECULAR
		return float4(specular, 1);
#endif
	}
	else
	{
#if DISABLE_BACKGROUND
		return float4(1,1,1,0);
#else
		outputColor = SkyBox.Sample(s0, -V).rgb * g_skyBoxMultiple;
#endif
	}
#if ENABLE_DIRECTIONAL_LIGHT & ENABLE_VOLUME_LIGHTING
	int volumeLightIterCount = _volumeLightIterCount;
	float volumeLightMaxDistance = _volumeLightMaxDistance;
	float volumeLightIntensity = _volumeLightIntensity;

	for (int i = 0; i < 1; i++)
	{
		int2 sx = uv * _widthHeight;
		uint randomState = RNG::RandomSeed(sx.x + sx.y * 2048 + g_RandomI);

		if (!any(Lightings[i].LightColor))continue;
		float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
		float volumeLightIterStep = volumeLightMaxDistance / volumeLightIterCount;
		volumeLightIterStep /= sqrt(clamp(1 - pow2(dot(Lightings[i].LightDir, -V)), 0.04, 1));
		float offset = RNG::Random01(randomState) * volumeLightIterStep;
		for (int j = 0; j < volumeLightIterCount; j++)
		{
			if (j * volumeLightIterStep + offset > camDist)
			{
				break;
			}
			float4 samplePos = float4(g_camPos - V * (volumeLightIterStep * j + offset), 1);
			float inShadow = pointInLight(0, samplePos.xyz);

			outputColor += inShadow * lightStrength * volumeLightIterStep * volumeLightIntensity;
		}
	}
#endif
	return float4(outputColor * _Brightness, 1);
}