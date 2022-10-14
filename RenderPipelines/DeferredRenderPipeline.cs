using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines;

[UIShow(name: "延迟渲染")]
public class DeferredRenderPipeline : RenderPipeline, IDisposable
{
    [AOV(AOVType.Color)]
    [Size("UnscaledOutput")]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    public Texture2D output;

    [AOV(AOVType.Depth)]
    [Size("Output")]
    [Format(ResourceFormat.D32_Float)]
    [AutoClear]
    public Texture2D depth;

    [Size("Output")]
    [Format(ResourceFormat.D32_Float)]
    public Texture2D depth2;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess2;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D gbuffer0;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D gbuffer1;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D gbuffer2;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D gbuffer3;


    [Size("BloomSize")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D intermedia1;//used by bloom
    [Size("BloomSize")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D intermedia2;//used by bloom
    [Size(2048, 2048, 9)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D intermedia3;//used by bloom

    [Size(4096, 4096)]
    [Format(ResourceFormat.D32_Float)]
    [AutoClear]
    public Texture2D _ShadowMap;

    [Size(2048, 2048, 9)]
    [Format(ResourceFormat.R32G32_Float)]
    [AutoClear]
    public Texture2D _HiZBuffer;

    [Size(128, 128)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [BRDFBaker]
    public Texture2D _BRDFLUT;

    [UIShow(name: "天空盒")]
    [Srgb]
    [Resource("adams_place_bridge_2k.jpg")]
    public Texture2D skyboxTexture;

    [Size(1024, 1024, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [CubeFrom2D(nameof(skyboxTexture))]
    [BakeDependency(nameof(skyboxTexture))]
    public TextureCube _SkyBox;

    [Size(512, 512, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [EnvironmentReflection(nameof(_SkyBox))]
    [BakeDependency(nameof(_SkyBox))]
    public TextureCube _Environment;

    [Size("GIBufferSize")]
    public GPUBuffer GIBuffer;

    [Size("GIBufferSize")]
    public GPUBuffer GIBufferWrite;

    #region Parameters
    [Indexable]
    [UIDragFloat(0.01f, 0, name: "天空盒亮度")]
    public float SkyLightMultiple = 3;

    [Indexable]
    [UIDragFloat(0.01f, 0, name: "亮度")]
    public float Brightness = 1;

    [Indexable]
    [UIShow(name: "启用雾")]
    public bool EnableFog;

    [Indexable]
    [UIColor(name: "雾颜色")]
    public Vector3 FogColor = new Vector3(0.4f, 0.4f, 0.6f);

    [Indexable]
    [UIDragFloat(0.001f, 0, name: "雾密度")]
    public float FogDensity = 0.005f;

    [Indexable]
    [UIDragFloat(0.1f, 0, name: "雾开始距离")]
    public float FogStartDistance = 5;

    [Indexable]
    //[UIDragFloat(0.1f, 0, name: "雾结束距离")]
    public float FogEndDistance = 100000;

    [UIShow(name: "启用泛光")]
    public bool EnableBloom;
    [Indexable]
    [UIDragFloat(0.01f, name: "泛光阈值")]
    public float BloomThreshold = 1.05f;
    [Indexable]
    [UIDragFloat(0.01f, name: "泛光强度")]
    public float BloomIntensity = 0.1f;

    [Indexable]
    [UIShow(name: "启用体积光")]
    public bool EnableVolumetricLighting;

    [Indexable]
    [UIDragInt(1, 1, 256, name: "体积光采样次数")]
    public int VolumetricLightingSampleCount = 16;

    [Indexable]
    [UIDragFloat(0.1f, name: "体积光距离")]
    public float VolumetricLightingDistance = 12;

    [Indexable]
    [UIDragFloat(0.1f, name: "体积光强度")]
    public float VolumetricLightingIntensity = 0.001f;

    [Indexable]
    [UIShow(name: "启用SSAO")]
    public bool EnableSSAO;

    [Indexable]
    [UIDragFloat(0.1f, 0, name: "AO距离")]
    public float AODistance = 1;

    [Indexable]
    [UIDragFloat(0.01f, 0.1f, name: "AO限制")]
    public float AOLimit = 0.3f;

    [Indexable]
    [UIDragInt(1, 0, 128, name: "AO光线采样次数")]
    public int AORaySampleCount = 32;

    [UIShow(name: "启用光线追踪")]
    public bool EnableRayTracing;

    [Indexable]
    [UIDragFloat(0.01f, 0, 5, name: "光线追踪反射质量")]
    public float RayTracingReflectionQuality = 1.0f;

    [Indexable]
    [UIDragFloat(0.01f, 0, 1.0f, name: "光线追踪反射阈值")]
    public float RayTracingReflectionThreshold = 0.5f;

    [UIShow(name: "更新全局光照")]
    public bool UpdateGI;

    [Indexable]
    [UIDragFloat(1.0f, name: "全局光照位置")]
    public Vector3 GIVolumePosition = new Vector3(0, 2.5f, 0);

    [Indexable]
    [UIDragFloat(1.0f, name: "全局光照范围")]
    public Vector3 GIVolumeSize = new Vector3(20, 5, 20);

    [Indexable]
    [UIShow(name: "使用全局光照")]
    public bool UseGI;

    [Indexable]
    [UIShow(name: "启用屏幕空间反射")]
    public bool EnableSSR;

    [UIShow(name: "启用TAA抗锯齿")]
    public bool EnableTAA;

    [UIDragFloat(0.01f, name: "TAA系数")]
    [Indexable]
    public float TAAFactor = 0.3f;

    [UISlider(0.5f, 2.0f, name: "渲染倍数")]
    public float RenderScale = 1;

    [UIShow(name: "调试渲染")]
    public DebugRenderType DebugRenderType;

    #endregion
    #region Material Parameters
    [Indexable]
    [UIShow(UIShowType.Material, "透明材质")]
    public bool IsTransparent;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "金属")]
    public float Metallic;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "粗糙")]
    public float Roughness = 0.8f;

    [Indexable]
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Material, "发光")]
    public float Emissive;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "高光")]
    public float Specular = 0.5f;

    [Indexable]
    [UISlider(0.0f, 1.0f, UIShowType.Material, "遮蔽")]
    public float AO = 1.0f;

    [UIShow(UIShowType.Material)]
    [PureColorBaker(1, 1, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    [Srgb]
    public Texture2D _Albedo;

    [UIShow(UIShowType.Material)]
    [PureColorBaker(1, 1, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Metallic;

    [UIShow(UIShowType.Material)]
    [PureColorBaker(1, 1, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Roughness;

    [UIShow(UIShowType.Material)]
    [Srgb]
    [PureColorBaker(1, 1, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Emissive;

    [Indexable]
    [UIShow(UIShowType.Material, "使用法线贴图")]
    public bool UseNormalMap;

    [UIShow(UIShowType.Material)]
    [PureColorBaker(0.5f, 0.5f, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Normal;

    [Indexable]
    [UIShow(UIShowType.Material, "使用Spa")]
    public bool UseSpa;

    [UIShow(UIShowType.Material)]
    [PureColorBaker(0, 0, 0, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    [Srgb]
    public Texture2D _Spa;

    #endregion
    #region Decal Parameters
    [Indexable]
    [UIShow(UIShowType.Decal, "启用贴花颜色")]
    public bool EnableDecalColor = true;
    [Srgb]
    [UIShow(UIShowType.Decal, "贴花颜色贴图")]
    public Texture2D DecalColorTexture;
    [Indexable]
    [UIShow(UIShowType.Decal, "启用贴花发光")]
    public bool EnableDecalEmissive;
    [Srgb]
    [UIShow(UIShowType.Decal, "贴花发光贴图")]
    public Texture2D DecalEmissiveTexture;
    [Indexable]
    [UIColor(UIShowType.Decal, "发光强度")]
    public Vector4 _DecalEmissivePower = new Vector4(1, 1, 1, 1);
    #endregion
    #region Light Parameters
    [Indexable]
    [UIColor(UIShowType.Light, "光照颜色")]
    public Vector3 LightColor = new Vector3(3, 3, 3);
    [Indexable]
    [UIDragFloat(0.1f, 0.1f, float.MaxValue, UIShowType.Light, "光照范围")]
    public float LightRange = 5.0f;

    [Indexable]
    [UIShow(UIShowType.Light, "光照类型")]
    public LightType LightType;

    #endregion
    #region Particle Parameters

    [Indexable]
    [UIDragInt(1, 0, 1000, UIShowType.Particle, "数量")]
    public int ParticleCount;
    [Indexable]
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "生命")]
    public Vector2 ParticleLife;
    [Indexable]
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "随机速度")]
    public Vector2 ParticleRandomSpeed;
    [Indexable]
    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "初始速度")]
    public Vector3 ParticleInitialSpeed;
    [Indexable]
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "尺寸")]
    public Vector2 ParticleScale;
    [Indexable]
    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "加速度")]
    public Vector3 ParticleAcceleration;

    [Srgb]
    [UIShow(UIShowType.Particle, "贴图")]
    public Texture2D ParticleTexture;
    [Indexable]
    [UIColor(UIShowType.Particle, "颜色")]
    public Vector4 ParticleColor = new Vector4(1, 1, 1, 1);
    [Indexable]
    [UIShow(UIShowType.Particle, "混合模式")]
    public BlendMode ParticleBlendMode;
    #endregion

    [SceneCapture("Camera")]
    public CameraData camera;

    [SceneCapture("Visual")]
    public IReadOnlyList<VisualComponent> Visuals;

    [SceneCapture("Particle")]
    public IReadOnlyList<(RenderMaterial, ParticleHolder)> Particles;

    RenderHelper renderHelper;

    Random random = new Random(0);
    public int outputWidth;
    public int outputHeight;

    CameraData historyCamera;

    public DeferredRenderPass deferredRenderPass = new DeferredRenderPass()
    {
        renderTarget = nameof(noPostProcess),
        depthStencil = nameof(depth),
    };

    public PostProcessPass postProcess = new PostProcessPass()
    {
        inputColor = nameof(noPostProcess),
        inputDepth = nameof(depth),
        output = nameof(output),
    };

    public TAAPass taaPass = new TAAPass()
    {
        target = nameof(noPostProcess),
        depth = nameof(depth),
        history = nameof(noPostProcess2),
        historyDepth = nameof(depth2),
    };

    public override void BeforeRender()
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.CPUSkinning = EnableRayTracing || UpdateGI;
        renderHelper.UpdateGPUResource();

        renderWrap.GetOutputSize(out outputWidth, out outputHeight);
        renderWrap.SetSize("UnscaledOutput", outputWidth, outputHeight);
        outputWidth = Math.Max((int)(outputWidth * RenderScale), 1);
        outputHeight = Math.Max((int)(outputHeight * RenderScale), 1);
        renderWrap.SetSize("Output", outputWidth, outputHeight);
        renderWrap.SetSize("HalfOutput", (outputWidth + 1) / 2, (outputHeight + 1) / 2);
        renderWrap.SetSize("QuarterOutput", (outputWidth + 3) / 4, (outputHeight + 3) / 4);
        renderWrap.SetSize("BloomSize", outputWidth * 256 / outputHeight, 256);
        renderWrap.SetSize("GIBufferSize", 589824, 1);
        renderWrap.texLoading = renderWrap.GetTex2DLoaded("loading.png");
        renderWrap.texError = renderWrap.GetTex2DLoaded("error.png");
        renderHelper.PushParameters(this);
    }

    public override void Render()
    {
        var camera = this.camera;
        if (EnableTAA)
        {
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputWidth, (float)(random.NextDouble() * 2 - 1) / outputHeight);
            camera = camera.GetJitter(jitterVector);
        }

        deferredRenderPass.Visuals = Visuals;
        deferredRenderPass.Brightness = Brightness;
        deferredRenderPass.rayTracing = EnableRayTracing;
        deferredRenderPass.updateGI = UpdateGI;
        deferredRenderPass.DebugRenderType = DebugRenderType;
        deferredRenderPass.Particles = Particles;
        postProcess.EnableBloom = EnableBloom;

        deferredRenderPass.SetCamera(camera);
        deferredRenderPass.Execute(renderHelper);

        if (EnableTAA)
        {
            taaPass.DebugRenderType = DebugRenderType;
            taaPass.SetCamera(historyCamera, this.camera);
            taaPass.SetProperties(outputWidth, outputHeight, TAAFactor);
            taaPass.Execute(renderHelper);
        }
        postProcess.Execute(renderHelper);

        renderWrap.Swap(nameof(noPostProcess), nameof(noPostProcess2));
        renderWrap.Swap(nameof(depth), nameof(depth2));
        historyCamera = this.camera;
        renderHelper.PopParameters();
    }

    public override void AfterRender()
    {
    }

    public void Dispose()
    {
        renderHelper?.Dispose();
        renderHelper = null;
    }
}
