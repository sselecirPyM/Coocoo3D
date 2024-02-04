using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines;

[Text(text: "前向渲染")]
public partial class ForwardRenderPipeline : RenderPipeline, IDisposable
{

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

    [SceneCapture("Camera")]
    public CameraData camera;

    [SceneCapture("Visual")]
    public IEnumerable<VisualComponent> Visuals;

    Random random = new Random(0);

    public int outputWidth;
    public int outputHeight;

    CameraData historyCamera;

    RenderHelper renderHelper;

    public override void BeforeRender()
    {
        renderHelper ??= new RenderHelper();
        renderHelper.renderWrap = renderWrap;
        renderHelper.renderPipeline = this;
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

        this.SetCamera(camera);
        this.Execute(renderHelper);

        if (taaPass.EnableTAA)
        {
            taaPass.DebugRenderType = DebugRenderType;
            taaPass.context = renderHelper;
            taaPass.Execute(historyCamera, this.camera);
        }

        postProcess.Execute(renderHelper);

        (depth, depth2) = (depth2, depth);
        (noPostProcess, noPostProcess2) = (noPostProcess2, noPostProcess);

        historyCamera = this.camera;
        renderHelper.PopParameters();
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
        drawSkyBox?.Dispose();
        drawSkyBox = null;
        taaPass?.Dispose();
        taaPass = null;
        drawShadowMap?.Dispose();
        drawShadowMap = null;
    }
}
