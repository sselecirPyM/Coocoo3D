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
    public class ForwardRenderPass
    {
        public DrawQuadPass drawSkyBox = new DrawQuadPass()
        {
            rs = "Cs",
            shader = "SkyBox.hlsl",
            renderTargets = new string[1],
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                "_SkyBox",
            },
            cbvs = new object[][]
            {
                new object []
                {
                    nameof(InvertViewProjection),
                    nameof(CameraPosition),
                    "SkyLightMultiple",
                    nameof(Brightness),
                }
            }
        };

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

        public DrawObjectPass drawObject = new DrawObjectPass()
        {
            shader = "PBRMaterial.hlsl",
            renderTargets = new string[1],
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
                ("EnableFog","ENABLE_FOG"),
            }
        };

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

        }

        public void SetRenderTarget(string renderTarget, string depthStencil)
        {
            this.renderTarget = renderTarget;
            this.depthStencil = depthStencil;
        }

        public void Execute(RenderWrap renderWrap)
        {
            drawSkyBox.renderTargets[0] = renderTarget;
            drawObject.renderTargets[0] = renderTarget;
            drawObject.depthStencil = depthStencil;
            var dls = renderWrap.directionalLights;
            if (dls.Count > 0)
            {
                ShadowMapVP = dls[0].GetLightingMatrix(InvertViewProjection, 0, 0.977f);
                ShadowMapVP1 = dls[0].GetLightingMatrix(InvertViewProjection, 0.977f, 0.993f);
                LightDir = dls[0].Direction;
                LightColor = dls[0].Color;
                drawObject.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
            }
            else
            {
                ShadowMapVP = Matrix4x4.Identity;
                ShadowMapVP1 = Matrix4x4.Identity;
                LightDir = Vector3.UnitZ;
                LightColor = Vector3.Zero;
            }

            renderWrap.PushParameters(this);
            if (dls.Count > 0)
            {
                renderWrap.ClearTexture("_ShadowMap");
                var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
                drawShadowMap.CBVPerObject[1] = ShadowMapVP;
                drawShadowMap.scissorViewport = new Rectangle(0, 0, shadowMap.width / 2, shadowMap.height / 2);
                drawShadowMap.Execute(renderWrap);
                drawShadowMap.CBVPerObject[1] = ShadowMapVP1;
                drawShadowMap.scissorViewport = new Rectangle(shadowMap.width / 2, 0, shadowMap.width / 2, shadowMap.height / 2);
                drawShadowMap.Execute(renderWrap);
            }
            drawSkyBox.Execute(renderWrap);

            drawObject.psoDesc.blendState = BlendState.None;
            drawObject.filter = FilterOpaque;
            drawObject.Execute(renderWrap);

            drawObject.psoDesc.blendState = BlendState.Alpha;
            drawObject.filter = FilterTransparent;
            drawObject.Execute(renderWrap);

            renderWrap.PopParameters();

            drawObject.keywords.Clear();
        }

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
