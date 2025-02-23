using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using RenderPipelines.LambdaRenderers;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace RenderPipelines;

[Text(text: "前向渲染")]
public partial class ForwardRenderPipeline : RenderPipeline, IDisposable
{
    //[Size(4096, 4096)]
    //[Format(ResourceFormat.D32_Float)]
    //[AutoClear]
    //public Texture2D _ShadowMap
    //{
    //    get => x__ShadowMap;
    //    set
    //    {
    //        x__ShadowMap = value;
    //        drawObject._ShadowMap = value;
    //    }
    //}
    //Texture2D x__ShadowMap;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess
    {
        get => x_noPostProcess;
        set
        {
            x_noPostProcess = value;
            postProcess.inputColor = value;
        }
    }
    Texture2D x_noPostProcess;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess2
    {
        get => x_noPostProcess2;
        set
        {
            x_noPostProcess2 = value;
        }
    }
    Texture2D x_noPostProcess2;

    [Size("BloomSize")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D intermedia1
    {
        get => x_intermedia1;
        set
        {
            x_intermedia1 = value;
            postProcess.intermedia1 = value;
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
            postProcess.intermedia2 = value;
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
            postProcess.intermedia3 = value;
        }
    }
    Texture2D x_intermedia3;

    [AOV(AOVType.Depth)]
    [Size("Output")]
    [Format(ResourceFormat.D32_Float)]
    [AutoClear]
    public Texture2D depth
    {
        get => x_depth;
        set
        {
            x_depth = value;
        }
    }
    Texture2D x_depth;

    [Size("Output")]
    [Format(ResourceFormat.D32_Float)]
    public Texture2D depth2
    {
        get => x_depth2;
        set
        {
            x_depth2 = value;
        }
    }
    Texture2D x_depth2;

    [AOV(AOVType.Color)]
    [Size("UnscaledOutput")]
    [Format(ResourceFormat.R8G8B8A8_UNorm)]
    [AutoClear]
    public Texture2D output
    {
        get => x_output;
        set
        {
            x_output = value;
            postProcess.output = value;
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

    [UITree()]
    public PostProcessPass postProcess
    {
        get => x_postProcess;
        set
        {
            x_postProcess = value;
        }
    }
    PostProcessPass x_postProcess = new();


    public RenderHelper context;

    [UISlider(0.5f, 2.0f, name: "渲染倍数")]
    public float RenderScale = 1;

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

    [SceneCapture("Camera")]
    public CameraData camera;

    [SceneCapture("Visual")]
    public IEnumerable<VisualComponent> Visuals;

    Random random = new Random(0);

    public int outputWidth;
    public int outputHeight;

    CameraData historyCamera;

    RenderHelper renderHelper;

    PipelineContext pipelineContext;
    TestResourceProvider testResourceProvider;

    public override void BeforeRender()
    {

        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.renderPipeline = this;
        renderHelper.UpdateGPUResource();
        renderHelper.UpdateRenderables();

        if (pipelineContext == null)
        {
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
        }
        pipelineContext.BeforeRender();

        renderWrap.GetOutputSize(out outputWidth, out outputHeight);
        renderWrap.SetSize("UnscaledOutput", outputWidth, outputHeight);
        outputWidth = (int)(outputWidth * RenderScale);
        outputHeight = (int)(outputHeight * RenderScale);
        renderWrap.SetSize("Output", outputWidth, outputHeight);
        renderWrap.SetSize("HalfOutput", (outputWidth + 1) / 2, (outputHeight + 1) / 2);
        renderWrap.SetSize("QuarterOutput", (outputWidth + 3) / 4, (outputHeight + 3) / 4);
        renderWrap.SetSize("BloomSize", outputWidth * 256 / outputHeight, 256);
        renderWrap.SetSize("GIBufferSize", 589824, 1);
        renderWrap.texError = renderWrap.rpc.mainCaches.GetTextureLoaded(Path.GetFullPath("error.png", renderWrap.BasePath));
    }

    public override void Render()
    {

        pipelineContext.BeforeRender();
        pipelineContext.ConfigRenderer<SuperPipelineConfig>();
        pipelineContext.Execute<SuperPipelineConfig>();

        (depth, depth2) = (depth2, depth);
        (noPostProcess, noPostProcess2) = (noPostProcess2, noPostProcess);
    }

    public override void AfterRender()
    {
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
        postProcess?.Dispose();
        postProcess = null;
        pipelineContext?.Dispose();
        pipelineContext = null;
        HiZPass?.Dispose();
        HiZPass = null;
        decalPass?.Dispose();
        decalPass = null;
    }
}
