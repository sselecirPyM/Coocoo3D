using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using Coocoo3D.UI.Attributes;
using System.Numerics;

namespace RenderPipelines
{
    [UIShow(name: "延迟渲染")]
    public class DeferredRenderPipeline : RenderPipeline
    {
        [AOV(AOVType.Color)]
        [Size("Output")]
        [Format(ResourceFormat.R8G8B8A8_UNorm)]
        public Texture2D output;

        [AOV(AOVType.Depth)]
        [Size("Output")]
        [Format(ResourceFormat.D32_Float)]
        [AutoClear]
        public Texture2D depth;

        [Size("Output")]
        [Format(ResourceFormat.R16G16B16A16_Float)]
        public Texture2D noPostProcess;

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


        [Size("HalfOutput")]
        [Format(ResourceFormat.R16G16B16A16_Float)]
        public Texture2D intermedia1;

        [Size(4096, 4096)]
        [Format(ResourceFormat.D32_Float)]
        [AutoClear]
        public Texture2D _ShadowMap;

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
        [UIDragFloat(0.1f, 0.1f, name: "AO限制")]
        public float AOLimit = 0.5f;

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

        #endregion

        [Indexable]
        public Matrix4x4 _ViewProjection = Matrix4x4.Identity;
        [Indexable]
        public Matrix4x4 _InvertViewProjection = Matrix4x4.Identity;

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

        public override void BeforeRender()
        {
            renderWrap.GetOutputSize(out int width, out int height);
            renderWrap.SetSize("Output", width, height);
            renderWrap.SetSize("HalfOutput", (width + 1) / 2, (height + 1) / 2);
            renderWrap.texLoading = renderWrap.GetTex2DLoaded("loading.png");
            renderWrap.texError = renderWrap.GetTex2DLoaded("error.png");
        }

        public override void Render()
        {
            deferredRenderPass.Brightness = Brightness;
            deferredRenderPass.rayTracing = EnableRayTracing;
            var camera = renderWrap.Camera;
            deferredRenderPass.SetCamera(camera);
            deferredRenderPass.Execute(renderWrap);

            postProcess.EnableBloom = EnableBloom;
            postProcess.Execute(renderWrap);
            _ViewProjection = camera.vpMatrix;
            _InvertViewProjection = camera.pvMatrix;
        }

        public override void AfterRender()
        {
        }
    }
}
