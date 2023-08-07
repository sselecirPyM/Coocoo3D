struct GBufferData
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

float2 _NormalEncode(float3 n)
{
	n.xy /= dot(1, abs(n));
	if (n.z <= 0)
	{
		n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? float2(1, 1) : float2(-1, -1));
	}
	return n.xy;
}

float3 _NormalDecode(float2 enc)
{
	float3 n = float3(enc, 1 - dot(1, abs(enc)));
	if (n.z < 0)
	{
		n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? float2(1, 1) : float2(-1, -1));
	}
	return normalize(n);
}

void GBufferDefault(inout GBufferData gbuffer)
{
	gbuffer.color0 = float4(0, 0, 0, 0);
	gbuffer.color1 = float4(0, 0, 0, 0);
	gbuffer.color2 = float4(0, 0, 0, 0);
	gbuffer.color3 = float4(0, 0, 0, 1);
}

void GBufferDiffuseSpecular(inout GBufferData gbuffer, float3 diffuse, float3 specular)
{
	gbuffer.color0.rgb = diffuse;
	gbuffer.color0.a = specular.r;
	gbuffer.color1.a = specular.g;
	gbuffer.color2.a = specular.b;
}

void GBufferMetallicAlbedo(inout GBufferData gbuffer, float metallic, float3 albedo)
{
	float3 diffuse = lerp(albedo * (1 - 0.04), 0, metallic);
	float3 specular = lerp(0.04, albedo, metallic);

	gbuffer.color0.rgb = diffuse;
	gbuffer.color0.a = specular.r;
	gbuffer.color1.a = specular.g;
	gbuffer.color2.a = specular.b;
}

void GBufferRoughness(inout GBufferData gbuffer, float roughness)
{
	gbuffer.color1.b = roughness;
}

void GBufferEmissive(inout GBufferData gbuffer, float3 emissive)
{
	gbuffer.color2.rgb = emissive;
}

void GBufferAO(inout GBufferData gbuffer, float ao)
{
	gbuffer.color3.r = ao;
}

void GBufferNormal(inout GBufferData gbuffer, float3 normal)
{
	gbuffer.color1.rg = _NormalEncode(normal);
}

float3 GBufferDiffuse(in GBufferData gbuffer)
{
	return gbuffer.color0.rgb;
}

float3 GBufferSpecular(in GBufferData gbuffer)
{
	return float3(gbuffer.color0.a, gbuffer.color1.a, gbuffer.color2.a);
}

float GBufferGetRoughness(in GBufferData gbuffer)
{
	return gbuffer.color1.b;
}

float3 GBufferGetEmissive(in GBufferData gbuffer)
{
	return gbuffer.color2.rgb;
}

float3 GBufferGetNormal(in GBufferData gbuffer)
{
	return normalize(_NormalDecode(gbuffer.color1.rg));
}

float GBufferGetAO(in GBufferData gbuffer)
{
	return gbuffer.color3.r;
}