#include "PBR.hlsli"
#include "SH.hlsli"
#include "Random.hlsli"

float Pow2(float x)
{
	return x * x;
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
cbuffer cb1 : register(b1)
{
#if ENABLE_POINT_LIGHT
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
#endif
}
Texture2D gbuffer0 :register(t0);
Texture2D gbuffer1 :register(t1);
Texture2D gbuffer2 :register(t2);
Texture2D gbuffer3 :register(t3);
TextureCube EnvCube : register (t4);
Texture2D gbufferDepth : register (t5);
Texture2D ShadowMap : register(t6);
TextureCube SkyBox : register (t7);
Texture2D BRDFLut : register(t8);
StructuredBuffer<SH9C> giBuffer : register(t9);
SamplerState s0 : register(s0);
SamplerComparisonState sampleShadowMap : register(s2);
SamplerState s3 : register(s3);

#define SH_RESOLUTION (16)
float3 NormalDecode(float2 enc)
{
	float4 nn = float4(enc * 2, 0, 0) + float4(-1, -1, 1, -1);
	float l = dot(nn.xyz, -nn.xyw);
	nn.z = l;
	nn.xy *= sqrt(max(l, 1e-6));
	return nn.xyz * 2 + float3(0, 0, -1);
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
		inShadow = ShadowMap.SampleCmpLevelZero(sampleShadowMap, shadowTexCoords * float2(0.5, 0.5), sPos.z).r;
	else
	{
		sPos = mul(pos1, LightMapVP1);
		sPos = sPos / sPos.w;
		shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
		shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
		if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
			inShadow = ShadowMap.SampleCmpLevelZero(sampleShadowMap, shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
	}
	return inShadow;
}
#endif
float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;

	float depth1 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer0Color = gbuffer0.SampleLevel(s3, uv, 0);
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float4 buffer2Color = gbuffer2.SampleLevel(s3, uv, 0);
	float4 buffer3Color = gbuffer3.SampleLevel(s3, uv, 0);


	float4 wPos = mul(float4(input.texcoord * 2 - 1, depth1, 1), g_mProjToWorld);
	wPos /= wPos.w;

	float3 V = normalize(g_camPos - wPos);

	float3 cam2Surf = g_camPos - wPos;
	float camDist = length(cam2Surf);

	float3 outputColor = float3(0, 0, 0);
	if (depth1 != 1.0)
	{
		float3 N = normalize(NormalDecode(buffer1Color.rg));
		int2 sx = uv * _widthHeight;
		uint randomState = RNG::RandomSeed(sx.x + sx.y * 2048 + g_RandomI);
		float AO = 1;
		float AOFactor = buffer3Color.r;
#if ENABLE_SSAO
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
#if DEBUG_AO
		return float4(AO, AO, AO, 1);
#endif
		float NdotV = saturate(dot(N, V));
		float roughness = buffer1Color.b;
		float alpha = roughness * roughness;
		float3 c_diffuse = buffer0Color.rgb;
		float3 c_specular = float3(buffer0Color.a, buffer1Color.a, buffer2Color.a);
		float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
		float3 GF = c_specular * AB.x + AB.y;
		float3 emissive = buffer2Color.rgb;

#if ENABLE_DIFFUSE
		float3 skyLight = EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple;
#ifndef ENABLE_GI
		outputColor += skyLight * c_diffuse * AO;
#else
		float3 shDiffuse = GetSH(giBuffer, SH_RESOLUTION, g_GIVolumePosition, g_GIVolumeSize, N, wPos, skyLight);
		outputColor += shDiffuse.rgb * c_diffuse * AO;
#endif
#endif
#if ENABLE_SPECULR & !RAY_TRACING
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF * AO;
#endif
#ifdef ENABLE_EMISSIVE
		outputColor += emissive;
#endif

#if DEBUG_DEPTH
		float _depth1 = pow(depth1, 2.2f);
		if (_depth1 < 1)
			return float4(_depth1, _depth1, _depth1, 1);
		else
			return float4(1, 0, 0, 1);
#endif
#if DEBUG_DIFFUSE
		return float4(c_diffuse, 1);
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
		return float4(c_specular, 1);
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

				inShadow = pointInLight(0, wPos.xyz);

#if ENABLE_DIFFUSE
				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
#else
				float diffuse_factor = 0;
#endif
#if ENABLE_SPECULR
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);
#else
				float3 specular_factor = 0;
#endif

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
		}
#endif//ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_POINT_LIGHT
		int shadowmapIndex = 0;
		for (int i = 0; i < POINT_LIGHT_COUNT; i++,shadowmapIndex += 6)
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
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				float shadowDepth = ShadowMap.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
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
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				float shadowDepth = ShadowMap.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
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
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				float shadowDepth = ShadowMap.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
				inShadow = (shadowDepth) > getDepth(abs(vl.z), lightRange * 0.001f, lightRange) ? 1 : 0;
			}

#if ENABLE_DIFFUSE
			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
#else
			float diffuse_factor = 0;
#endif
#if ENABLE_SPECULR
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);
#else
			float3 specular_factor = 0;
#endif
			outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
		}
#endif//ENABLE_POINT_LIGHT
#if ENABLE_FOG
		outputColor = lerp(pow(max(_fogColor, 1e-6), 2.2f), outputColor, 1 / exp(max((camDist - _startDistance) / 10, 0.00001) * _fogDensity));
#endif
	}
	else
	{
		outputColor = SkyBox.Sample(s0, -V).rgb * g_skyBoxMultiple;
	}
#if ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_VOLUME_LIGHTING
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
#endif
	return float4(outputColor * _Brightness, 1);
}