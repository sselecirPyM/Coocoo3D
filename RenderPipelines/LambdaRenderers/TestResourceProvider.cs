using Newtonsoft.Json;
using RenderPipelines.LambdaPipe;
using RenderPipelines.Utility;
using System;
using System.IO;

namespace RenderPipelines.LambdaRenderers
{
    public class TestResourceProvider : IPipelineResourceProvider, IDisposable
    {
        public RenderHelper RenderHelper { get; set; }

        public RayTracingShader GetRayTracingShader()
        {
            if (_rayTracingShader != null)
                return _rayTracingShader;

            var path1 = Path.GetFullPath("RayTracing.json", RenderHelper.renderPipelineView.BasePath);
            using var filestream = File.OpenRead(path1);
            _rayTracingShader = ReadJsonStream<RayTracingShader>(filestream);
            return _rayTracingShader;
        }

        public RayTracingShader _rayTracingShader { get; set; }

        public BloomPass bloomPass = new BloomPass();

        public SRGBConvertPass srgbConvert = new SRGBConvertPass();

        public GenerateMipPass generateMipPass = new GenerateMipPass();

        public VariantShader shader_skybox = new VariantShader(
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

        public VariantShader shader_shadow = new VariantShader(
    """
cbuffer cb1 : register(b0)
{
	float4x4 g_transform;
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
};

float4 vsmain(VSSkinnedIn input) : SV_POSITION
{
	return mul(float4(input.Pos, 1), g_transform);
}
""", "vsmain", null, null, "shadowMap.hlsl");


        [Flags]
        public enum Keyword_shader_TAA
        {
            None = 0,
            DEBUG_TAA = 1,
        }
        public VariantComputeShader<Keyword_shader_TAA> shader_TAA = new VariantComputeShader<Keyword_shader_TAA>(
    """
float _pow2(float x)
{
    return x * x;
}
cbuffer cb0 : register(b0)
{
    float4x4 g_mWorldToProj;
    float4x4 g_mProjToWorld;
    float4x4 g_mWorldToProj1;
    float4x4 g_mProjToWorld1;
    int2 _widthHeight;
    float g_cameraFarClip;
    float g_cameraNearClip;
    float mixFactor;
};
Texture2D _depth : register(t0);
Texture2D _previousResult : register(t1);
Texture2D _previousDepth : register(t2);
SamplerState s0 : register(s0);
SamplerState s3 : register(s3);

RWTexture2D<float4> _result : register(u0);

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
    float2 uv = ((float2) dtid.xy + 0.5) / (float2) _widthHeight;
    float2 reproj = uv * 2 - 1;
    reproj.y = -reproj.y;

    float2 pixelSize = 1.0f / _widthHeight;

    float4 sourceColor = _result[dtid.xy];
    float3 color = sourceColor.rgb;

    float weight = 1;

    float depth2 = _depth.SampleLevel(s0, uv, 0).r;

    float4 wPos2 = mul(float4(reproj, depth2, 1), g_mProjToWorld);
    wPos2 /= wPos2.w;
    float4 posX2 = mul(wPos2, g_mWorldToProj1);
    float2 uv2 = posX2.xy / posX2.w;
    uv2.x = uv2.x * 0.5 + 0.5;
    uv2.y = 0.5 - uv2.y * 0.5;
    bool aa = false;
    float minz = 1;
    float maxz = 0;
    float minz1 = 1;
    float maxz1 = 0;
    float threshold = (100 + _pow2(depth2) * 800 + g_cameraNearClip * 10) / _widthHeight.y;
    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            float depth = _depth.SampleLevel(s0, uv + float2(x, y) * pixelSize, 0).r;
            minz = min(minz, depth);
            maxz = max(maxz, depth);
            float4 wPos = mul(float4(reproj + float2(x, y) * pixelSize * 2, depth, 1), g_mProjToWorld);
            wPos /= wPos.w;

            float4 posX1 = mul(wPos, g_mWorldToProj1);
            float2 uv1 = posX1.xy / posX1.w;
            uv1.x = uv1.x * 0.5 + 0.5;
            uv1.y = 0.5 - uv1.y * 0.5;

            float depth1 = _previousDepth.SampleLevel(s0, uv2 + float2(x, y) * pixelSize, 0).r;
            float4 wPos1 = mul(float4(posX1.xy / posX1.w, depth1, 1), g_mProjToWorld1);
            wPos1 /= wPos1.w;

            float4 projX = mul(wPos1, g_mWorldToProj);
            float depth1X = projX.z / projX.w;
            minz1 = min(minz1, depth1X);
            maxz1 = max(maxz1, depth1X);

            if (distance(wPos.xyz, wPos1.xyz) < threshold)
            {
                aa = true;
            }
        }
    float mid1 = (minz1 + maxz1) / 2;
    if (mid1 > maxz || mid1 < minz)
    {
        aa = false;
    }
    if (aa)
    {
        color *= mixFactor;
        weight *= mixFactor;
        color += _previousResult.SampleLevel(s0, uv2, 0).rgb;
        weight += 1;
    }
    color /= weight;
#if DEBUG_TAA
	if (weight == 1)
	{
		_result[dtid.xy] = float4(0.75, 0.5, 0.75, 1);
		return;
	}
#endif

    _result[dtid.xy] = float4(color, sourceColor.a);
}
""", "csmain");

        [Flags]
        public enum DrawDecalFlag
        {
            None = 0,
            ENABLE_DECAL_COLOR = 1,
            ENABLE_DECAL_EMISSIVE = 2
        }
        public VariantShader<DrawDecalFlag> shader_drawDecal = new VariantShader<DrawDecalFlag>(
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

        public VariantComputeShader shader_hiz1 = new VariantComputeShader("""

Texture2D<float> input : register(t0);

RWTexture2D<float2> hiz : register(u0);
cbuffer cb0 : register(b0) {
	int2 inputSize;
}

bool validate(int2 position)
{
	return all(inputSize > position);
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	if (!validate((int2(dtid.xy))))
		return;
	float4 val;

	val.x = input[(dtid.xy * 2) + int2(0, 0)];

	for (int i = 1; i < 4; i++)
	{
		int2 index2 = (dtid.xy * 2) + int2((i >> 1) & 1, i & 1);
		if (validate(index2))
			val[i] = input[index2];
		else
			val[i] = val.x;
	}

	float min1 = min(min(val.x, val.y), min(val.z, val.w));
	float max1 = max(max(val.x, val.y), max(val.z, val.w));
	hiz[dtid.xy] = float2(min1, max1);
}
			
""", "csmain", null);
       public VariantComputeShader shader_hiz2 = new VariantComputeShader("""

Texture2D<float2> input : register(t0);

RWTexture2D<float2> hiz : register(u0);
cbuffer cb0 : register(b0) {
	int2 inputSize;
}

bool validate(int2 position)
{
	return all(inputSize > position);
}

[numthreads(8, 8, 1)]
void csmain(uint3 dtid : SV_DispatchThreadID)
{
	if (!validate((int2(dtid.xy))))
		return;

	float2 col[4];
	col[0].xy = input[(dtid.xy * 2) + int2(0, 0)].rg;

	for (int i = 1; i < 4; i++)
	{
		if (validate((dtid.xy * 2) + int2((i >> 1) & 1, i & 1)))
			col[i].rg = input[(dtid.xy * 2) + int2((i >> 1) & 1, i & 1)].rg;
		else
			col[i].rg = col[0].rg;
	}

	float min1 = col[0].x;
	float max1 = col[0].y;
	for(i=1; i < 4; i++)
	{
		min1 = min(min1, col[i].x);
		max1 = max(max1, col[i].y);
	}

	hiz[dtid.xy] = float2(min1, max1);
}
			
""", "csmain", null);


        public static T ReadJsonStream<T>(Stream stream)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamReader reader1 = new StreamReader(stream);
            return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
        }

        public void Dispose()
        {
            bloomPass?.Dispose();
            bloomPass = null;
            srgbConvert?.Dispose();
            srgbConvert = null;
            generateMipPass?.Dispose();
            generateMipPass = null;


            shader_skybox?.Dispose();
            shader_skybox = null;
            shader_shadow?.Dispose();
            shader_shadow = null;
            shader_TAA?.Dispose();
            shader_TAA = null;
            shader_drawDecal?.Dispose();
            shader_drawDecal = null;

            shader_hiz1?.Dispose();
            shader_hiz1 = null;
            shader_hiz2?.Dispose();
            shader_hiz2 = null;


        }
    }
}
