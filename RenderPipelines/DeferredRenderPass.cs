using Caprice.Attributes;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class DeferredRenderPass
    {
        public DrawObjectPass drawShadowMap = new DrawObjectPass()
        {
            shader = "ShadowMap.hlsl",
            depthStencil = "_ShadowMap",
            rs = "CCs",
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
                depthBias = 1000,
                slopeScaledDepthBias = 1.0f,
            },
            enablePS = false,
            CBVPerObject = new object[]
            {
                null,
                "ShadowMapVP",
            },
        };

        public DrawObjectPass drawGBuffer = new DrawObjectPass()
        {
            shader = "DeferredGBuffer.hlsl",
            renderTargets = new string[]
            {
                "gbuffer0",
                "gbuffer1",
                "gbuffer2",
                "gbuffer3",
            },
            depthStencil = null,
            rs = "CCCssssssssss",
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                "_Albedo",
                "_Metallic",
                "_Roughness",
                "_Emissive",
                "_Normal",
            },
            CBVPerObject = new object[]
            {
                null,
                nameof(ViewProjection),
                "Metallic",
                "Roughness",
                "Emissive",
                "Specular",
                "AO",
            },
            AutoKeyMap =
            {
                ("UseNormalMap","USE_NORMAL_MAP"),
            },
            filter = FilterOpaque,
        };
        public DrawQuadPass finalPass = new DrawQuadPass()
        {
            rs = "CCCsssssssssss",
            shader = "DeferredFinal.hlsl",
            renderTargets = new string[1],
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                "gbuffer0",
                "gbuffer1",
                "gbuffer2",
                "gbuffer3",
                "_Environment",
                null,
                "_ShadowMap",
                "_SkyBox",
                "_BRDFLUT",

            },
            cbvs = new object[][]
            {
                new object []
                {
                    nameof(ViewProjection),
                    nameof(InvertViewProjection),
                    nameof(Far),
                    nameof(Near),
                    nameof(Fov),
                    nameof(AspectRatio),
                    nameof(CameraPosition),
                    "SkyLightMultiple",
                    "FogColor",
                    "FogDensity",
                    "FogStartDistance",
                    "FogEndDistance",
                    nameof(OutputSize),
                    nameof(Brightness),
                    "VolumetricLightingSampleCount",
                    "VolumetricLightingDistance",
                    "VolumetricLightingIntensity",
                    nameof(ShadowMapVP),
                    nameof(ShadowMapVP1),
                    nameof(LightDir),
                    0,
                    nameof(LightColor),
                    0,
                    Vector3.Zero,
                    "AODistance",
                    Vector3.Zero,
                    "AOLimit",
                    "AORaySampleCount"

                }
            },
            AutoKeyMap =
            {
                ("EnableFog","ENABLE_FOG"),
                ("EnableSSAO","ENABLE_SSAO")
            }
        };

        public DrawObjectPass drawObjectTransparent = new DrawObjectPass()
        {
            shader = "PBRMaterial.hlsl",
            renderTargets = new string[1],
            depthStencil = null,
            rs = "CCCssssssssss",
            psoDesc = new PSODesc()
            {
                blendState = BlendState.Alpha,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                "_Albedo",
                "_Metallic",
                "_Roughness",
                "_Emissive",
                "_ShadowMap",
                "_Environment",
                "_BRDFLUT",
                "_Normal",
            },
            CBVPerObject = new object[]
            {
                null,
                "Metallic",
                "Roughness",
                "Emissive",
                "Specular",
            },
            CBVPerPass = new object[]
            {
                nameof(ViewProjection),
                nameof(View),
                nameof(CameraPosition),
                nameof(Brightness),
                nameof(Far),
                nameof(Near),
                nameof(Fov),
                nameof(AspectRatio),
                nameof(ShadowMapVP),
                nameof(ShadowMapVP1),
                nameof(LightDir),
                0,
                nameof(LightColor),
                0,
                "SkyLightMultiple",
                "FogColor",
                "FogDensity",
                "FogStartDistance",
                "FogEndDistance",
            },
            AutoKeyMap =
            {
                ("UseNormalMap","USE_NORMAL_MAP"),
            },
            filter = FilterTransparent,
        };

        RayTracingPass rayTracingPass = new RayTracingPass()
        {
            srvs = new string[]
            {
                null,
                "_Environment",
                "_BRDFLUT",
                null,//nameof(depth),
                "gbuffer0",
                "gbuffer1",
                "gbuffer2",
                "_ShadowMap",
            },
            RayTracingShader = "RayTracing.json",
        };

        public bool rayTracing = true;

        [Indexable]
        public Matrix4x4 ViewProjection;
        [Indexable]
        public Matrix4x4 View;
        [Indexable]
        public Matrix4x4 Projection;
        [Indexable]
        public Matrix4x4 InvertViewProjection;
        [Indexable]
        public Vector3 CameraPosition;

        [Indexable]
        public float Far;
        [Indexable]
        public float Near;
        [Indexable]
        public float Fov;
        [Indexable]
        public float AspectRatio;


        [Indexable]
        public Matrix4x4 ShadowMapVP;
        [Indexable]
        public Matrix4x4 ShadowMapVP1;

        [Indexable]
        public Vector3 LightDir;
        [Indexable]
        public Vector3 LightColor;

        [Indexable]
        public float Brightness = 1;

        [Indexable]
        public (int, int) OutputSize;

        public string renderTarget;
        public string depthStencil;

        public void SetCamera(CameraData camera)
        {
            Far = camera.far;
            Near = camera.near;
            Fov = camera.Fov;
            AspectRatio = camera.AspectRatio;

            ViewProjection = camera.vpMatrix;
            View = camera.vMatrix;
            Projection = camera.pMatrix;
            InvertViewProjection = camera.pvMatrix;
            CameraPosition = camera.Position;

            rayTracingPass.SetCamera(camera);
        }

        public void SetRenderTarget(string renderTarget, string depthStencil)
        {
            this.renderTarget = renderTarget;
            this.depthStencil = depthStencil;
        }

        public void Execute(RenderWrap renderWrap)
        {
            drawGBuffer.depthStencil = depthStencil;
            drawObjectTransparent.depthStencil = depthStencil;
            drawObjectTransparent.renderTargets[0] = renderTarget;
            finalPass.renderTargets[0] = renderTarget;
            finalPass.srvs[5] = depthStencil;

            rayTracingPass.RenderTarget = "gbuffer2";
            rayTracingPass.srvs[3] = depthStencil;

            var dls = renderWrap.directionalLights;
            if (dls.Count > 0)
            {
                var dl = dls[0];
                ShadowMapVP = dl.GetLightingMatrix(InvertViewProjection, 0, 0.977f);
                ShadowMapVP1 = dl.GetLightingMatrix(InvertViewProjection, 0.977f, 0.993f);
                LightDir = dl.Direction;
                LightColor = dl.Color;
                finalPass.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
                if (true.Equals(renderWrap.GetIndexableValue("EnableVolumetricLighting")))
                {
                    finalPass.keywords.Add(("ENABLE_VOLUME_LIGHTING", "1"));
                }
            }
            else
            {
                ShadowMapVP = Matrix4x4.Identity;
                ShadowMapVP1 = Matrix4x4.Identity;
                LightDir = Vector3.UnitZ;
                LightColor = Vector3.Zero;
            }
            if (rayTracing)
            {
                finalPass.keywords.Add(("RAY_TRACING", "1"));
            }
            if (debugKeywords.TryGetValue(renderWrap.DebugRenderType, out string debugKeyword))
            {
                finalPass.keywords.Add((debugKeyword, "1"));
            }

            var outputTex = renderWrap.GetRenderTexture2D(renderTarget);
            OutputSize = (outputTex.width, outputTex.height);

            renderWrap.PushParameters(this);
            if (dls.Count > 0)
            {
                renderWrap.SetRenderTarget(null, "_ShadowMap", false, true);
                var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
                drawShadowMap.CBVPerObject[1] = ShadowMapVP;
                drawShadowMap.scissorViewport = new Rectangle(0, 0, shadowMap.width / 2, shadowMap.height / 2);
                drawShadowMap.Execute(renderWrap);
                drawShadowMap.CBVPerObject[1] = ShadowMapVP1;
                drawShadowMap.scissorViewport = new Rectangle(shadowMap.width / 2, 0, shadowMap.width / 2, shadowMap.height / 2);
                drawShadowMap.Execute(renderWrap);
            }
            drawGBuffer.Execute(renderWrap);
            if (rayTracing)
            {
                rayTracingPass.RayTracing = rayTracing;
                rayTracingPass.Execute(renderWrap);
            }
            finalPass.Execute(renderWrap);
            drawObjectTransparent.Execute(renderWrap);
            renderWrap.PopParameters();

            drawGBuffer.keywords.Clear();
            drawObjectTransparent.keywords.Clear();
            finalPass.keywords.Clear();
        }


        static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
        {
            { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
            { DebugRenderType.Bitangent,"DEBUG_BITANGENT"},
            { DebugRenderType.Depth,"DEBUG_DEPTH"},
            { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
            { DebugRenderType.DiffuseRender,"DEBUG_DIFFUSE_RENDER"},
            { DebugRenderType.Emissive,"DEBUG_EMISSIVE"},
            { DebugRenderType.Normal,"DEBUG_NORMAL"},
            { DebugRenderType.Position,"DEBUG_POSITION"},
            { DebugRenderType.Roughness,"DEBUG_ROUGHNESS"},
            { DebugRenderType.Specular,"DEBUG_SPECULAR"},
            { DebugRenderType.SpecularRender,"DEBUG_SPECULAR_RENDER"},
            { DebugRenderType.Tangent,"DEBUG_TANGENT"},
            { DebugRenderType.UV,"DEBUG_UV"},
        };

        static bool FilterOpaque(RenderWrap renderWrap, MeshRenderable renderable, List<(string, string)> keywords)
        {
            if (true.Equals(renderWrap.GetIndexableValue("IsTransparent", renderable.material)))
            {
                return false;
            }
            return true;
        }

        static bool FilterTransparent(RenderWrap renderWrap, MeshRenderable renderable, List<(string, string)> keywords)
        {
            if (true.Equals(renderWrap.GetIndexableValue("IsTransparent", renderable.material)))
            {
                return true;
            }
            return false;
        }
    }
}
