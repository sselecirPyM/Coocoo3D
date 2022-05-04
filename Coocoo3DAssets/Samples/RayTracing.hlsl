#include "Random.hlsli"
#include "PBR.hlsli"
#include "SH.hlsli"
bool equls(in float3 a, in float3 b)
{
	return (a.x == b.x) && (a.y == b.y) && (a.z == b.z);
}

bool equls(in float4 a, in float4 b)
{
	return (a.x == b.x) && (a.y == b.y) && (a.z == b.z) && (a.w == b.w);
}

float4 Pow4(float4 x)
{
	return x * x * x * x;
}
float3 Pow4(float3 x)
{
	return x * x * x * x;
}
float2 Pow4(float2 x)
{
	return x * x * x * x;
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

	float3 TangentX = { 1 + Sign * a * pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

float4 ImportanceSampleGGX(float2 E, float a2)
{
	float Phi = 2 * COO_PI * E.x;
	float CosTheta = sqrt((1 - E.y) / (1 + (a2 - 1) * E.y));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float d = (CosTheta * a2 - CosTheta) * CosTheta + 1;
	float D = a2 / (COO_PI * d * d);
	float PDF = D * CosTheta;

	return float4(H, PDF);
}

float3 NormalDecode(float2 enc)
{
	float4 nn = float4(enc * 2, 0, 0) + float4(-1, -1, 1, -1);
	float l = dot(nn.xyz, -nn.xyw);
	nn.z = l;
	nn.xy *= sqrt(max(l, 1e-6));
	return nn.xyz * 2 + float3(0, 0, -1);
}

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float3 LightColor;
	float useless;
};

static const float4 backgroundColor = float4(0.4, 0.6, 0.2, 1.0);

RaytracingAccelerationStructure gRtScene : register(t0);
TextureCube EnvCube : register (t1);
Texture2D BRDFLut : register(t2);
Texture2D gbufferDepth : register (t3);
Texture2D gbuffer0 :register(t4);
Texture2D gbuffer1 :register(t5);
Texture2D gbuffer2 :register(t6);
Texture2D ShadowMap0 : register(t7);
StructuredBuffer<SH9C> giBuffer : register(t8);

RWTexture2D<float4> gOutput : register(u0);
RWStructuredBuffer<SH9C> giResult : register(u1);
#define SH_RESOLUTION (16)

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);

cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_camPos;
	float    g_skyBoxMultiple;
	float3   g_GIVolumePosition;
	float    g_RayTracingReflectionQuality;
	float3   g_GIVolumeSize;
	int      g_RandomI;
	float    g_RayTracingReflectionThreshold;
};
cbuffer cb1 : register(b0, space1)
{
	float4x4 g_mWorld;
	float4x4 g_mWorldToProj1;
	float4x4 g_mProjToWorld1;
	float4x4 LightMapVP;
	float4x4 LightMapVP1;
	LightInfo Lightings[1];
	float _Metallic;
	float _Roughness;
	float _Emissive;
	float _Specular;
};

struct RayPayload
{
	float3 color;
	float3 direction;
};

[shader("raygeneration")]
void rayGen()
{
	uint3 launchIndex = DispatchRaysIndex();
	uint3 launchDim = DispatchRaysDimensions();

	float2 xy = launchIndex.xy + 0.5f; // center in the middle of the pixel.
	float2 uv = xy / launchDim.xy;
	float2 screenPos = uv * 2.0 - 1.0;
	screenPos.y = -screenPos.y;
	float depth = gbufferDepth.SampleLevel(s3, uv, 0).r;

	if (depth == 1.0) return;
	float4 buffer0Color = gbuffer0.SampleLevel(s3, uv, 0);
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float4 buffer2Color = gbuffer2.SampleLevel(s3, uv, 0);

	float4 world = mul(float4(screenPos, depth, 1), g_mProjToWorld);
	world /= world.w;

	float3 V = normalize(g_camPos - world.xyz);
	float3 N = normalize(NormalDecode(buffer1Color.rg));
	float NdotV = saturate(dot(N, V));
	float roughness = buffer1Color.b;
	float alpha = roughness * roughness;
	float3 c_specular = float3(buffer0Color.a, buffer1Color.a, buffer2Color.a);
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	uint randomState = RNG::RandomSeed(DispatchRaysIndex().x + DispatchRaysIndex().y * 8192 + g_RandomI);
	int sampleCount = 1 + (int)(roughness * 256 * g_RayTracingReflectionQuality * GF);
	float3 specReflectColor = float3(0, 0, 0);
	float weight = 0;
	if (g_RayTracingReflectionThreshold > roughness)
	{
		uint2 rnd1 = uint2(RNG::Random(randomState), RNG::Random(randomState));
		for (int i = 0; i < sampleCount; i++)
		{
			float2 E = Hammersley(i, sampleCount, rnd1);
			float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(roughness)).xyz, N);
			float3 L = 2 * dot(V, H) * H - V;

			float NdotL = saturate(dot(N, L));
			L = normalize(L);

			if (NdotL > 0)
			{
				RayDesc ray;
				ray.Origin = world.xyz;
				ray.Direction = L;
				ray.TMin = 0.01;
				ray.TMax = 10000;

				RayPayload payload;
				payload.color = float3(0, 0, 0);
				payload.direction = L;
				TraceRay(gRtScene,
					RAY_FLAG_NONE /*rayFlags*/,
					0xFF,
					0 /* ray index*/,
					0 /* Multiplies */,
					0 /* Miss index */,
					ray,
					payload);
				specReflectColor += payload.color * NdotL;
				weight += NdotL;
			}
		}
		specReflectColor = specReflectColor / max(weight, 1e-4) * GF;
		gOutput[launchIndex.xy] = float4(specReflectColor, 0);
	}
	else
	{
		gOutput[launchIndex.xy] = float4(EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF, 0);
	}
}

[shader("miss")]
void miss(inout RayPayload payload)
{
	payload.color = EnvCube.SampleLevel(s0, payload.direction, 0) * g_skyBoxMultiple;
}

StructuredBuffer<uint> MeshIndexs : register(t0, space1);
StructuredBuffer<float3> Positions : register(t1, space1);
StructuredBuffer<float3> Normals : register(t2, space1);
StructuredBuffer<float2> UVs : register(t3, space1);
Texture2D<float4> Albedo : register(t4, space1);
Texture2D<float4> Emissive : register(t5, space1);
Texture2D<float4> Metallic : register(t6, space1);
Texture2D<float4> Roughness : register(t7, space1);

[shader("closesthit")]
void closestHit(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attr)
{
#define GET_PROPERTY(x) (x[meshIndexs[0]]*vertexWeight[0] + x[meshIndexs[1]]*vertexWeight[1] + x[meshIndexs[2]]*vertexWeight[2])
	uint3 meshIndexs;
	meshIndexs[0] = MeshIndexs[PrimitiveIndex() * 3 + 0];
	meshIndexs[1] = MeshIndexs[PrimitiveIndex() * 3 + 1];
	meshIndexs[2] = MeshIndexs[PrimitiveIndex() * 3 + 2];

	float3 vertexWeight;
	vertexWeight[0] = 1 - attr.barycentrics.x - attr.barycentrics.y;
	vertexWeight[1] = attr.barycentrics.x;
	vertexWeight[2] = attr.barycentrics.y;

	float3 position = mul(float4(GET_PROPERTY(Positions), 1), g_mWorld).xyz;
	float3 N = mul(normalize(GET_PROPERTY(Normals)), g_mWorld).xyz;
	float2 uv = GET_PROPERTY(UVs);
	float4 albedo = Albedo.SampleLevel(s1, uv, 0);
	float3 emissive = Emissive.SampleLevel(s1, uv, 0) * _Emissive;

	float4 metallic1 = Metallic.SampleLevel(s1, uv, 0);
	float4 roughness1 = Roughness.SampleLevel(s1, uv, 0);
	float roughness = max(_Roughness * roughness1.g, 0.002);
	float alpha = roughness * roughness;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic * metallic1.b);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic * metallic1.b);
	float3 V = -payload.direction;

	float NdotV = saturate(dot(N, V));
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	payload.color += (EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF).rgb;
	payload.color += emissive;

	float3 skyLight = (EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple).rgb;

#ifndef ENABLE_GI
	payload.color += skyLight * c_diffuse;
#else
	float3 shDiffuse = GetSH(giBuffer, SH_RESOLUTION, g_GIVolumePosition, g_GIVolumeSize, N, position, skyLight);
	payload.color += shDiffuse * c_diffuse;
#endif
#if ENABLE_DIRECTIONAL_LIGHT
	for (int i = 0; i < 1; i++)
	{
		if (!any(Lightings[i].LightColor))continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
			if (i == 0)
			{
				float4 sPos;
				float2 shadowTexCoords;
				sPos = mul(float4(position, 1), LightMapVP);
				sPos = sPos / sPos.w;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 0.5), sPos.z).r;
				else
				{
					sPos = mul(float4(position, 1), LightMapVP1);
					sPos = sPos / sPos.w;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (all(sPos.xy >= -1) && all(sPos.xy <= 1))
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
				}
			}
			float3 L = normalize(Lightings[i].LightDir);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));
			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

			payload.color += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
		}
	}
#endif //ENABLE_DIRECTIONAL_LIGHT

	//payload.color = albedo;
	//payload.color = float4(position, 1);
	//payload.color = float4(N * 0.5 + 0.5, 1);
}


float Pow2(float x) {
	return x * x;
}

float3 GetDir(float theta, float phi) {
	float3 result;
	float s_theta, c_theta, s_phi, c_phi;
	sincos(theta, s_theta, c_theta);
	sincos(phi, s_phi, c_phi);
	result.z = c_theta;
	result.x = s_theta * c_phi;
	result.y = s_theta * s_phi;
	return result;
}

[shader("raygeneration")]
void rayGenGI()
{
	uint3 launchIndex = DispatchRaysIndex();
	uint3 launchDim = DispatchRaysDimensions();
	int shIndex = launchIndex.x + launchIndex.y * SH_RESOLUTION + launchIndex.z * SH_RESOLUTION * SH_RESOLUTION;
	uint randomState = RNG::RandomSeed(launchIndex.x + launchIndex.y * 2048 + launchIndex.z * 4194304 + g_RandomI);
	float3 position = (clamp((launchIndex + RNG::Random01F3(randomState) - 0.5f) / float3(SH_RESOLUTION, SH_RESOLUTION, SH_RESOLUTION), 0, 1) - 0.5f) * g_GIVolumeSize + g_GIVolumePosition;

	const int sampleCountX = 16;
	const int sampleCountY = 16;

	const uint shcCount = 9;
	const float updateFactor = 0.03;
	const float A = 4.0 * COO_PI / (sampleCountX * sampleCountY) * updateFactor;

	SH9C shResult;
	for (int i = 0; i < shcCount; i++)
	{
		shResult.values[i] = giBuffer[shIndex].values[i] * (1 - updateFactor);
	}
	float4 colors[sampleCountX * sampleCountY];
	for (int i = 0; i < sampleCountX * sampleCountY; i++)
	{
		colors[i] = float4(0, 0, 0, 0);
	}


	const int c_sampleCount = 128;
	uint2 rnd1 = uint2(RNG::Random(randomState), RNG::Random(randomState));
	for (int k = 0; k < c_sampleCount; k++)
	{
		float2 E = Hammersley(k, c_sampleCount, rnd1);
		float3 vec1 = UniformSampleSphere(E);

		RayDesc ray;
		//ray.Origin = world.xyz;
		ray.Origin = position;
		ray.Direction = vec1;
		ray.TMin = 0.05;
		ray.TMax = 10000;

		RayPayload payload;
		payload.color = float3(0, 0, 0);
		payload.direction = vec1;
		TraceRay(gRtScene,
			RAY_FLAG_NONE /*rayFlags*/,
			0xFF,
			0 /* ray index*/,
			0 /* Multiplies */,
			0 /* Miss index */,
			ray,
			payload);

		for (int i = 0; i < sampleCountX; i++)
			for (int j = 0; j < sampleCountY; j++)
			{
				float theta = acos(1 - i * 2.0 / (sampleCountX - 1 + 1e-3));
				float phi = 2 * COO_PI * (j * 1 / (sampleCountY - 1 + 1e-3));
				float3 N = normalize(GetDir(theta, phi));
				float NdotL = dot(vec1, N);
				if (NdotL > 0)
				{
					float4 color = max(float4(payload.color * NdotL, 0), 0);
					colors[i + j * sampleCountX] += color;
				}
			}
	}

	for (int i = 0; i < sampleCountX; i++)
		for (int j = 0; j < sampleCountY; j++)
		{
			float theta = acos(1 - i * 2.0 / (sampleCountX - 1 + 1e-3));
			float phi = 2 * COO_PI * (j * 1 / (sampleCountY - 1 + 1e-3));
			float3 N = normalize(GetDir(theta, phi));
			float4 color = colors[i + j * sampleCountX] * 2 / c_sampleCount;
			AddSH9Color(shResult, N, color * A);
		}

	giResult[shIndex] = shResult;
}


[shader("raygeneration")]
void rayGenPathTracing()
{
	uint3 launchIndex = DispatchRaysIndex();
	uint3 launchDim = DispatchRaysDimensions();

	float2 xy = launchIndex.xy + 0.5f; // center in the middle of the pixel.
	float2 uv = xy / launchDim.xy;
	float2 screenPos = uv * 2.0 - 1.0;
	screenPos.y = -screenPos.y;
	float depth = gbufferDepth.SampleLevel(s3, uv, 0).r;

	if (depth == 1.0) return;
	float4 buffer0Color = gbuffer0.SampleLevel(s3, uv, 0);
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float4 buffer2Color = gbuffer2.SampleLevel(s3, uv, 0);

	float4 world = mul(float4(screenPos, depth, 1), g_mProjToWorld);
	world /= world.w;

	float3 V = normalize(g_camPos - world.xyz);
	float3 N = normalize(NormalDecode(buffer1Color.rg));
	float NdotV = saturate(dot(N, V));
	float roughness = buffer1Color.b;
	float alpha = roughness * roughness;
	float3 c_specular = float3(buffer0Color.a, buffer1Color.a, buffer2Color.a);
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	uint randomState = RNG::RandomSeed(DispatchRaysIndex().x + DispatchRaysIndex().y * 8192 + g_RandomI);
	int sampleCount = 1 + (int)(roughness * 256 * g_RayTracingReflectionQuality * GF);
	float3 specReflectColor = float3(0, 0, 0);
	float weight = 0;
	if (g_RayTracingReflectionThreshold > roughness)
	{
		uint2 rnd1 = uint2(RNG::Random(randomState), RNG::Random(randomState));
		for (int i = 0; i < sampleCount; i++)
		{
			float2 E = Hammersley(i, sampleCount, rnd1);
			float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(roughness)).xyz, N);
			float3 L = 2 * dot(V, H) * H - V;

			float NdotL = saturate(dot(N, L));
			L = normalize(L);

			if (NdotL > 0)
			{
				RayDesc ray;
				ray.Origin = world.xyz;
				ray.Direction = L;
				ray.TMin = 0.01;
				ray.TMax = 10000;

				RayPayload payload;
				payload.color = float3(0, 0, 0);
				payload.direction = L;
				TraceRay(gRtScene,
					RAY_FLAG_NONE /*rayFlags*/,
					0xFF,
					0 /* ray index*/,
					0 /* Multiplies */,
					0 /* Miss index */,
					ray,
					payload);
				specReflectColor += payload.color * NdotL;
				weight += NdotL;
			}
		}
		specReflectColor = specReflectColor / max(weight, 1e-4) * GF;
		gOutput[launchIndex.xy] = float4(specReflectColor, 0);
	}
	else
	{
		gOutput[launchIndex.xy] = float4(EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF, 0);
	}
}