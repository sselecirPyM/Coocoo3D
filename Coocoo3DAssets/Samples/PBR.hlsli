static const float COO_PI = 3.141592653589793238f;
static const float COO_EPSILON = 1e-5f;

float4 pow5(float4 x)
{
	return x * x * x * x * x;
}
float3 pow5(float3 x)
{
	return x * x * x * x * x;
}
float2 pow5(float2 x)
{
	return x * x * x * x * x;
}
float pow5(float x)
{
	return x * x * x * x * x;
}

float4 pow2(float4 x)
{
	return x * x;
}
float3 pow2(float3 x)
{
	return x * x;
}
float2 pow2(float2 x)
{
	return x * x;
}
float pow2(float x)
{
	return x * x;
}

// Shlick's approximation of Fresnel
// https://en.wikipedia.org/wiki/Schlick%27s_approximation
float3 Fresnel_Shlick(in float3 f0, in float3 f90, in float x)
{
	return f0 + (f90 - f0) * pow5(1.f - x);
}

// Burley B. "Physically Based Shading at Disney"
// SIGGRAPH 2012 Course: Practical Physically Based Shading in Film and Game Production, 2012.
float Diffuse_Burley(in float NdotL, in float NdotV, in float LdotH, in float roughness)
{
	float fd90 = 0.5f + 2.f * roughness * LdotH * LdotH;
	return Fresnel_Shlick(1, fd90, NdotL).x * Fresnel_Shlick(1, fd90, NdotV).x;
}

float Diffuse_Lambert(in float NdotL, in float NdotV, in float LdotH, in float roughness)
{
	return NdotL;
}

// GGX specular D (normal distribution)
// https://www.cs.cornell.edu/~srm/publications/EGSR07-btdf.pdf
float Specular_D_GGX(in float alpha, in float NdotH)
{
	const float alpha2 = alpha * alpha;
	const float lower = (NdotH * alpha2 - NdotH) * NdotH + 1;
	return alpha2 / max(COO_EPSILON, COO_PI * lower * lower);
}

// Schlick-Smith specular G (visibility) with Hable's LdotH optimization
// http://www.cs.virginia.edu/~jdl/bib/appearance/analytic%20models/schlick94b.pdf
// http://graphicrants.blogspot.se/2013/08/specular-brdf-reference.html
float G_Shlick_Smith_Hable(float alpha, float LdotH)
{
	return rcp(lerp(LdotH * LdotH, 1, alpha * alpha * 0.25f));
}

// A microfacet based BRDF.
//
// alpha:           This is roughness * roughness as in the "Disney" PBR model by Burley et al.
//
// specularColor:   The F0 reflectance value - 0.04 for non-metals, or RGB for metals. This follows model 
//                  used by Unreal Engine 4.
//
// NdotV, NdotL, LdotH, NdotH: vector relationships between,
//      N - surface normal
//      V - eye normal
//      L - light normal
//      H - half vector between L & V.
float3 Specular_BRDF(in float alpha, in float3 specularColor, in float NdotV, in float NdotL, in float LdotH, in float NdotH)
{
	float specular_D = Specular_D_GGX(alpha, NdotH);
	float3 specular_F = Fresnel_Shlick(specularColor, 1, LdotH);
	float specular_G = G_Shlick_Smith_Hable(alpha, LdotH);

	return specular_D * specular_F * specular_G;
}

float3 Fresnel_SchlickRoughness(float cosTheta, float3 F0, float roughness)
{
	return F0 + (max(1.0 - roughness, F0) - F0) * pow5(1.0 - cosTheta);
}