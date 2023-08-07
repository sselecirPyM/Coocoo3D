using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.MetaRender;

[Text(text: "元渲染")]
public class MetaRenderPipeline : RenderPipeline, IDisposable
{
    public override IDictionary<UIShowType, ICloneable> materialTypes { get; } =
        new Dictionary<UIShowType, ICloneable> {
            {
                UIShowType.Decal, new DecalMaterial()
                {
                    DecalColorTexture = new Texture2D(),
                    DecalEmissiveTexture = new Texture2D()
                }
            },
            {
                UIShowType.Light,new LightMaterial()
                {

                }
            },
            {
                UIShowType.Particle,new ParticleMaterial()
                {
                    ParticleTexture = new Texture2D()
                }
            }
        };

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
    [Resource("adams_place_bridge_2k.jpg")]
    public Texture2D skyboxTexture;

    [Size(1024, 1024, 6, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [CubeFrom2D(nameof(skyboxTexture))]
    [BakeDependency(nameof(skyboxTexture))]
    public Texture2D _SkyBox;

    [Size(512, 512, 6, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [EnvironmentReflection(nameof(_SkyBox))]
    [BakeDependency(nameof(_SkyBox))]
    public Texture2D _Environment;

    [Size("GIBufferSize")]
    public GPUBuffer GIBuffer;

    [Size("GIBufferSize")]
    public GPUBuffer GIBufferWrite;

    #region Parameters

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
    public Texture2D _Spa;

    #endregion

    [SceneCapture("Camera")]
    public CameraData camera;

    [SceneCapture("Visual")]
    public IReadOnlyList<VisualComponent> Visuals;

    RenderHelper renderHelper;

    Random random = new Random(0);
    public int outputWidth;
    public int outputHeight;

    CameraData historyCamera;

    [UITree]
    public MetaDeferredRenderPass deferredRenderPass = new MetaDeferredRenderPass()
    {
        renderTarget = nameof(noPostProcess),
        depthStencil = nameof(depth),
    };

    [UITree]
    public PostProcessPass postProcess = new PostProcessPass()
    {
        inputColor = nameof(noPostProcess),
        inputDepth = nameof(depth),
        output = nameof(output),
    };

    [UITree]
    public TAAPass taaPass = new TAAPass()
    {
        target = nameof(noPostProcess),
        depth = nameof(depth),
        history = nameof(noPostProcess2),
        historyDepth = nameof(depth2),
    };


    MetaRenderContext metaRenderContext = new MetaRenderContext();

    void Light(VisualComponent v)
    {
        var material = renderHelper.GetObject<ICloneable>(v.material.Parameters, materialTypes[UIShowType.Light]);
        LightMaterial lm = material as LightMaterial;
        if (lm == null)
            return;
        var lightType = lm.LightType;
        Vector3 lightColor = lm.LightColor;
        if (lightType == LightType.Directional)
        {
            var directionnalLightData = new DirectionalLightData
            {
                Color = lightColor,
                Direction = Vector3.Transform(-Vector3.UnitZ, v.transform.rotation),
                Rotation = v.transform.rotation,
            };

            metaRenderContext.originData.Add(directionnalLightData);
        }
        else if (lightType == LightType.Point)
        {
            float range = lm.LightRange;

            var pointLightData = new PointLightData1
            {
                Color = lightColor,
                Position = v.transform.position,
                Range = range,
            };

            metaRenderContext.originData.Add(pointLightData);
        }
    }

    public override void BeforeRender()
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.CPUSkinning = deferredRenderPass.EnableRayTracing || deferredRenderPass.UpdateGI;
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

    void MetaPrepare()
    {
        metaRenderContext.deltaTime = renderWrap.rpc.DeltaTime;
        if (metaRenderContext.processor == null)
        {
            metaRenderContext.processor = new List<object>()
            {
                new ObjectProcessor(),
                //new RenderTransferProcessor()
            };
        }
        metaRenderContext.Clear();


        metaRenderContext.originData.Add(camera);
        foreach (VisualComponent v in Visuals)
        {
            if (v.UIShowType == UIShowType.Light)
                Light(v);
            else if (v.UIShowType == UIShowType.Decal)
            {
                metaRenderContext.originData.Add(new DecalRenderable
                {
                    transform = v.transform,
                    material = renderHelper.GetObject<ICloneable>(v.material.Parameters, materialTypes[UIShowType.Decal]),
                });
            }
            else if (v.UIShowType == UIShowType.Particle)
            {
                metaRenderContext.originData.Add(new ParticleRenderable()
                {
                    id = v.id,
                    transform = v.transform.GetMatrix(),
                    material = renderHelper.GetObject<ICloneable>(v.material.Parameters, materialTypes[UIShowType.Particle]),
                });
            }
        }

        foreach (var renderer in renderWrap.rpc.renderers)
        {
            foreach (var r in renderHelper.GetRenderables(renderer))
            {
                metaRenderContext.originData.Add(r);
            }
        }

        foreach (var renderer in renderWrap.rpc.meshRenderers)
        {
            foreach (var r in renderHelper.GetRenderables(renderer))
            {
                metaRenderContext.originData.Add(r);
            }
        }

        foreach (dynamic processor in metaRenderContext.processor)
        {
            processor.Process(metaRenderContext);
        }
        deferredRenderPass.MetaRenderContext = metaRenderContext;
    }

    public override void Render()
    {
        var camera = this.camera;
        if (taaPass.EnableTAA)
        {
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputWidth, (float)(random.NextDouble() * 2 - 1) / outputHeight);
            camera = camera.GetJitter(jitterVector);
        }

        MetaPrepare();

        deferredRenderPass.DebugRenderType = DebugRenderType;

        deferredRenderPass.SetCamera(camera);
        deferredRenderPass.Execute(renderHelper);

        if (taaPass.EnableTAA)
        {
            taaPass.DebugRenderType = DebugRenderType;
            taaPass.SetCamera(historyCamera, this.camera);
            taaPass.SetProperties(outputWidth, outputHeight);
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
