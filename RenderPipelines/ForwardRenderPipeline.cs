using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using RenderPipelines.LambdaRenderers;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.IO;

namespace RenderPipelines;

[Text(text: "前向渲染")]
public partial class ForwardRenderPipeline : RenderPipeline, IDisposable
{
    Texture2D _ShadowMap { get; set; }

    Texture2D noPostProcess { get; set; }
    Texture2D noPostProcess2 { get; set; }

    [Size("BloomSize")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D intermedia1
    {
        get => x_intermedia1;
        set
        {
            x_intermedia1 = value;
        }
    }
    Texture2D x_intermedia1;

    [Size("BloomSize")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D intermedia2
    {
        get => x_intermedia2;
        set
        {
            x_intermedia2 = value;
        }
    }
    Texture2D x_intermedia2;

    [Size(2048, 2048, 9)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [AutoClear]
    public Texture2D intermedia3
    {
        get => x_intermedia3;
        set
        {
            x_intermedia3 = value;
        }
    }
    Texture2D x_intermedia3;

    Texture2D depth { get; set; }
    Texture2D depth2 { get; set; }

    [Size("UnscaledOutput")]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [AutoClear]
    public Texture2D output
    {
        get => x_output;
        set
        {
            x_output = value;
        }
    }
    Texture2D x_output;

    [Size(128, 128)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [BRDFBaker()]
    public Texture2D _BRDFLUT
    {
        get => x__BRDFLUT;
        set
        {
            x__BRDFLUT = value;
        }
    }
    Texture2D x__BRDFLUT;

    [Size(1024, 1024, 6, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [CubeFrom2D(nameof(skyboxTexture))]
    [BakeDependency(nameof(skyboxTexture))]
    public Texture2D _SkyBox
    {
        get => x__SkyBox;
        set
        {
            x__SkyBox = value;
        }
    }
    Texture2D x__SkyBox;

    [Size(512, 512, 6, 6)]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    [EnvironmentReflection(nameof(_SkyBox))]
    [BakeDependency(nameof(_SkyBox))]
    public Texture2D _Environment
    {
        get => x__Environment;
        set
        {
            x__Environment = value;
        }
    }
    Texture2D x__Environment;

    [UIShow(name: "天空盒")]
    [Resource("adams_place_bridge_2k.jpg")]
    public Texture2D skyboxTexture
    {
        get => x_skyboxTexture;
        set
        {
            x_skyboxTexture = value;
        }
    }
    Texture2D x_skyboxTexture;


    public RenderHelper context;

    [UISlider(0.5f, 2.0f, name: "渲染倍数")]
    public float RenderScale = 1;

    [UISlider(512, 8192, name: "阴影贴图尺寸")]
    public float ShadowMapSize = 4096;

    [UISlider(0.2f, 0.9f, name: "阴影近距离")]
    public float ShadowNearDistance = 0.2f;
    [UISlider(0.90f, 0.999f, name: "阴影中距离")]
    public float ShadowMidDistance = 0.93f;
    [UISlider(0.90f, 0.999f, name: "阴影远距离")]
    public float ShadowFarDistance = 0.991f;

    [UIShow(name: "调试渲染")]
    public DebugRenderType DebugRenderType;

    #region Material Parameters

    [UIShow(UIShowType.Material)]
    [PureColorBaker(1, 1, 1, 1)]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [Size(32, 32)]
    public Texture2D _Albedo, _Metallic, _Roughness, _Emissive;

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

    Random random = new Random(0);

    public int outputWidth;
    public int outputHeight;

    CameraData camera;
    CameraData historyCamera;

    RenderHelper renderHelper;

    PipelineContext pipelineContext;
    TestResourceProvider testResourceProvider;

    public override void Config(RenderPipelineView renderPipelineView)
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderPipelineView = renderPipelineView;
        renderHelper.renderPipeline = this;


        testResourceProvider = new TestResourceProvider()
        {
            RenderHelper = renderHelper
        };
        var builder = new PipelineBuilder();
        builder.AddRenderers();
        builder.AddRenderer<SuperPipelineConfig>(this.Config, this.Execute);
        builder.AddPipelineResourceProvider(testResourceProvider);
        builder.AddPipelineResourceProvider(new TextureResourceProvider()
        {
            RenderHelper = renderHelper
        });

        pipelineContext = new PipelineContext();
        pipelineContext.PipelineBuilder = builder;
        renderHelper.InitializeResources();
        renderPipelineView.beforeRender += () =>
        {
            renderHelper.UpdateGPUResource();
            renderHelper.UpdateRenderables();


            renderPipelineView.GetOutputSize(out outputWidth, out outputHeight);
            renderPipelineView.SetSize("UnscaledOutput", outputWidth, outputHeight);
            outputWidth = (int)(outputWidth * RenderScale);
            outputHeight = (int)(outputHeight * RenderScale);
            renderPipelineView.SetSize("Output", outputWidth, outputHeight);
            renderPipelineView.SetSize("HalfOutput", (outputWidth + 1) / 2, (outputHeight + 1) / 2);
            renderPipelineView.SetSize("QuarterOutput", (outputWidth + 3) / 4, (outputHeight + 3) / 4);
            renderPipelineView.SetSize("BloomSize", outputWidth * 256 / outputHeight, 256);
            //renderPipelineView.SetSize("GIBufferSize", 589824, 1);
            renderPipelineView.texError = renderPipelineView.rpc.mainCaches.GetTextureLoaded(Path.GetFullPath("error.png", renderPipelineView.BasePath));

            renderPipelineView.SetAOV(AOVType.Color, output);
            renderPipelineView.SetAOV(AOVType.Depth, depth);


            int size = (int)ShadowMapSize;
            size -= 1;
            size |= size >> 1;
            size |= size >> 2;
            size |= size >> 4;
            size |= size >> 8;
            size |= size >> 16;
            size += 1;

            GIBuffer = renderPipelineView.ConfigBuffer("GIBuffer", (rt) =>
            {
                rt.width = 589824;
            });
            GIBufferWrite = renderPipelineView.ConfigBuffer("GIBufferWrite", (rt) =>
            {
                rt.width = 589824;
            });

            _ShadowMap = renderPipelineView.ConfigTexture("_ShadowMap", (rt) =>
            {
                rt.width = size;
                rt.height = size;
                rt.autoClear = true;
                rt.autoClearDepth = 1.0f;
                rt.resourceFormat = ResourceFormat.D32_Float;
            });

            Action<RenderTextureUsage> usage = (rt) =>
            {
                rt.width = outputWidth;
                rt.height = outputHeight;
                rt.resourceFormat = ResourceFormat.R16G16B16A16_Float;
            };
            noPostProcess = renderPipelineView.ConfigTexture("noPostProcess", usage);
            noPostProcess2 = renderPipelineView.ConfigTexture("noPostProcess2", usage);

            Action<RenderTextureUsage> bufferUsage = (rt) =>
            {
                rt.width = outputWidth;
                rt.height = outputHeight;
                rt.resourceFormat = ResourceFormat.R16G16B16A16_Float;
                rt.autoClear = true;
            };
            gbuffer0 = renderPipelineView.ConfigTexture("gbuffer0", bufferUsage);
            gbuffer1 = renderPipelineView.ConfigTexture("gbuffer1", bufferUsage);
            gbuffer2 = renderPipelineView.ConfigTexture("gbuffer2", bufferUsage);
            gbuffer3 = renderPipelineView.ConfigTexture("gbuffer3", bufferUsage);

            depth = renderPipelineView.ConfigTexture("depth", (rt) =>
            {
                rt.width = outputWidth;
                rt.height = outputHeight;
                rt.resourceFormat = ResourceFormat.D32_Float;
                rt.autoClear = true;
            });
            depth2 = renderPipelineView.ConfigTexture("depth2", (rt) =>
            {
                rt.width = outputWidth;
                rt.height = outputHeight;
                rt.resourceFormat = ResourceFormat.D32_Float;
            });
        };

        renderPipelineView.render += () =>
        {
            pipelineContext.ConfigRenderer<SuperPipelineConfig>();
            pipelineContext.Execute<SuperPipelineConfig>();

            SwapTexture("depth", "depth2");
            SwapTexture("noPostProcess", "noPostProcess2");
        };
    }

    void SwapTexture(string texture1, string texture2)
    {
        var usage1 = renderPipelineView.RenderTextures[texture1];
        var usage2 = renderPipelineView.RenderTextures[texture2];
        (usage1.texture, usage2.texture) = (usage2.texture, usage1.texture);
    }

    void SwapBuffer(string texture1, string texture2)
    {
        var usage1 = renderPipelineView.RenderTextures[texture1];
        var usage2 = renderPipelineView.RenderTextures[texture2];
        (usage1.gpuBuffer, usage2.gpuBuffer) = (usage2.gpuBuffer, usage1.gpuBuffer);
    }

    public override object UIMaterial(RenderMaterial material)
    {
        if (material.Type == UIShowType.Light)
        {
            return DictExt.ConvertToObject<LightMaterial>(material.Parameters, renderHelper);
        }
        else if (material.Type == UIShowType.Decal)
        {
            return DictExt.ConvertToObject<DecalMaterial>(material.Parameters, renderHelper);
        }
        else if (material.Type == UIShowType.Material)
        {
            var showMaterial = DictExt.ConvertToObject<ModelMaterial>(material.Parameters);
            showMaterial._Albedo ??= _Albedo;
            showMaterial._Metallic ??= _Metallic;
            showMaterial._Roughness ??= _Roughness;
            showMaterial._Emissive ??= _Emissive;
            showMaterial._Normal ??= _Normal;
            showMaterial._Spa ??= _Spa;
            return showMaterial;
        }
        return null;
    }

    public void Dispose()
    {
        renderHelper?.Dispose();
        renderHelper = null;
        pipelineContext?.Dispose();
        pipelineContext = null;
    }
}
