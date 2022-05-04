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
    public class ForwardRenderPipeline : RenderPipeline
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

        [UIShow]
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

        #region Material Parameters
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

        [UIShow(UIShowType.Material)]
        [PureColorBaker(0.5f, 0.5f, 1, 1)]
        [Format(ResourceFormat.R8G8B8A8_UNorm)]
        [Size(32, 32)]
        public Texture2D _Normal;

        #endregion

        public DrawQuadPass postProcess = new DrawQuadPass()
        {
            shader = "PostProcessing.hlsl",
            renderTargets = new string[]
            {
                nameof(output)
            },
            //depthStencil = null,
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                nameof(noPostProcess),
            },
            cbvs = new object[][]
            {
                new object []
                {

                }
            }
        };

        ForwardRenderPass forwordRenderPass = new ForwardRenderPass()
        {
            renderTarget = nameof(noPostProcess),
            depthStencil = nameof(depth),
        };

        public override void BeforeRender()
        {
            renderWrap.GetOutputSize(out int width, out int height);
            renderWrap.SetSize("Output", width, height);
            renderWrap.texLoading = renderWrap.GetTex2DLoaded("loading.png");
            renderWrap.texError = renderWrap.GetTex2DLoaded("error.png");
        }

        public override void Render()
        {
            forwordRenderPass.Brightness = Brightness;
            var camera = renderWrap.Camera;
            forwordRenderPass.SetCamera(camera);
            forwordRenderPass.Execute(renderWrap);
            postProcess.Execute(renderWrap);
        }

        public override void AfterRender()
        {
        }
    }
}
