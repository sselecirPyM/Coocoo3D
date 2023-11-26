using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines;

[Text(text: "前向渲染")]
public class ForwardRenderPipeline : RenderPipeline, IDisposable
{
    public override IDictionary<UIShowType, ICloneable> materialTypes { get; } =
        new Dictionary<UIShowType, ICloneable> { };

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

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess;

    [Size("Output")]
    [Format(ResourceFormat.R16G16B16A16_Float)]
    public Texture2D noPostProcess2;

    [Size(4096, 4096)]
    [Format(ResourceFormat.D32_Float)]
    [AutoClear]
    public Texture2D _ShadowMap;

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

    [UISlider(0.5f, 2.0f, name: "渲染倍数")]
    public float RenderScale = 1;

    [UIShow(name: "调试渲染")]
    public DebugRenderType DebugRenderType;

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

    [SceneCapture("Camera")]
    public CameraData camera;

    [SceneCapture("Visual")]
    public IEnumerable<VisualComponent> Visuals;

    Random random = new Random(0);

    public int outputWidth;
    public int outputHeight;

    CameraData historyCamera;

    [UITree]
    public ForwardRenderPass forwordRenderPass = new ForwardRenderPass()
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

    RenderHelper renderHelper;

    public override void BeforeRender()
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.CPUSkinning = false;
        renderHelper.UpdateGPUResource();

        renderWrap.GetOutputSize(out outputWidth, out outputHeight);
        renderWrap.SetSize("UnscaledOutput", outputWidth, outputHeight);
        outputWidth = (int)(outputWidth * RenderScale);
        outputHeight = (int)(outputHeight * RenderScale);
        renderWrap.SetSize("Output", outputWidth, outputHeight);
        renderWrap.SetSize("HalfOutput", (outputWidth + 1) / 2, (outputHeight + 1) / 2);
        renderWrap.SetSize("QuarterOutput", (outputWidth + 3) / 4, (outputHeight + 3) / 4);
        renderWrap.SetSize("BloomSize", outputWidth * 256 / outputHeight, 256);
        renderWrap.texError = renderWrap.GetTex2DLoaded("error.png");
        renderHelper.PushParameters(this);
    }

    public override void Render()
    {
        var camera = this.camera;
        if (taaPass.EnableTAA)
        {
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputWidth, (float)(random.NextDouble() * 2 - 1) / outputHeight);
            camera = camera.GetJitter(jitterVector);
        }

        forwordRenderPass.Visuals = Visuals;
        forwordRenderPass.DebugRenderType = DebugRenderType;

        forwordRenderPass.SetCamera(camera);
        forwordRenderPass.Execute(renderHelper);

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
