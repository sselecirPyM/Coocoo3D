using Caprice.Attributes;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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
                depthBias = 2000,
                slopeScaledDepthBias = 1.5f,
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
                "GIBuffer",
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
                    "GIVolumePosition",
                    "AODistance",
                    "GIVolumeSize",
                    "AOLimit",
                    "AORaySampleCount",
                    nameof(RandomI),
                    nameof(Split),
                },
                new object[]
                {
                    null
                }
            },
            AutoKeyMap =
            {
                ("EnableFog","ENABLE_FOG"),
                ("EnableSSAO","ENABLE_SSAO"),
                ("UseGI","ENABLE_GI"),
            }
        };

        public Random random = new Random(0);

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
                "GIBuffer",
            },
            CBVPerObject = new object[]
            {
                null,
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
                nameof(Split),
                "GIVolumePosition",
                "GIVolumeSize",
            },
            AutoKeyMap =
            {
                ("EnableFog","ENABLE_FOG"),
                ("UseNormalMap","USE_NORMAL_MAP"),
                ("UseGI","ENABLE_GI"),
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
                "GIBuffer",
            },
            RayTracingShader = "RayTracing.json",
        };

        public bool rayTracing;
        public bool updateGI;

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

        [Indexable]
        public int RandomI;

        public string renderTarget;
        public string depthStencil;

        [Indexable]
        public int Split;

        public DebugRenderType DebugRenderType;

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

            RandomI = random.Next();

            var dls = renderWrap.directionalLights;
            var pls = renderWrap.pointLights;
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
            if (debugKeywords.TryGetValue(DebugRenderType, out string debugKeyword))
            {
                finalPass.keywords.Add((debugKeyword, "1"));
                drawObjectTransparent.keywords.Add((debugKeyword, "1"));
            }

            var outputTex = renderWrap.GetRenderTexture2D(renderTarget);
            OutputSize = (outputTex.width, outputTex.height);

            renderWrap.PushParameters(this);
            if (dls.Count > 0)
            {
                renderWrap.SetRenderTarget(null, "_ShadowMap", false, true);
                var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
                int width = shadowMap.width;
                int height = shadowMap.height;
                drawShadowMap.CBVPerObject[1] = ShadowMapVP;
                drawShadowMap.scissorViewport = new Rectangle(0, 0, width / 2, height / 2);
                drawShadowMap.Execute(renderWrap);
                drawShadowMap.CBVPerObject[1] = ShadowMapVP1;
                drawShadowMap.scissorViewport = new Rectangle(width / 2, 0, width / 2, height / 2);
                drawShadowMap.Execute(renderWrap);
            }

            Split = SplitTest(pls.Count * 12);
            byte[] pointLightData = null;
            if (pls.Count > 0)
            {
                DrawPointShadow(renderWrap);
                pointLightData = ArrayPool<byte>.Shared.Rent(pls.Count * 32);
                var spanWriter = new SpanWriter<PointLightData>(MemoryMarshal.Cast<byte, PointLightData>(pointLightData));
                for (int i = 0; i < pls.Count; i++)
                {
                    PointLightData pointLightData1 = new PointLightData
                    {
                        Position = pls[i].Position,
                        Color = pls[i].Color,
                        Range = pls[i].Range
                    };
                    spanWriter.Write(pointLightData1);
                }
                finalPass.cbvs[1][0] = (pointLightData, pls.Count * 32);
                finalPass.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
                finalPass.keywords.Add(("POINT_LIGHT_COUNT", pls.Count.ToString()));

                drawObjectTransparent.CBVPerObject[1] = (pointLightData, pls.Count * 32);
                drawObjectTransparent.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
                drawObjectTransparent.keywords.Add(("POINT_LIGHT_COUNT", pls.Count.ToString()));
            }
            drawGBuffer.Execute(renderWrap);
            if (rayTracing || updateGI)
            {
                rayTracingPass.RayTracing = rayTracing;
                rayTracingPass.RayTracingGI = updateGI;
                rayTracingPass.Execute(renderWrap);
            }
            finalPass.Execute(renderWrap);
            drawObjectTransparent.Execute(renderWrap);
            renderWrap.PopParameters();

            if (pls.Count > 0)
            {
                finalPass.cbvs[1][0] = null;
                drawObjectTransparent.CBVPerObject[1] = null;
                ArrayPool<byte>.Shared.Return(pointLightData);
            }
            drawGBuffer.keywords.Clear();
            drawObjectTransparent.keywords.Clear();
            finalPass.keywords.Clear();
        }

        static Matrix4x4 GetShadowMapMatrix(Vector3 pos, Vector3 dir, Vector3 up, float near, float far)
        {
            return Matrix4x4.CreateLookAt(pos, pos + dir, up)
             * Matrix4x4.CreatePerspectiveFieldOfView(1.57079632679f, 1, near, far);
        }

        static Rectangle GetRectangle(int index, int split, int width, int height)
        {
            float xOffset = (float)(index % split) / split;
            float yOffset = (float)(index / split) / split;
            float size = 1.0f / split;

            int x = (int)(width * xOffset);
            int y = (int)(height * (yOffset + 0.5f));
            int sizeX1 = (int)(width * size);
            int sizeY1 = (int)(height * size);

            return new Rectangle(x, y, sizeX1, sizeY1);
        }

        void DrawPointShadow(RenderWrap renderWrap)
        {
            int index = 0;
            var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
            int width = shadowMap.width;
            int height = shadowMap.height;
            foreach (var pl in renderWrap.pointLights)
            {
                var lightRange = pl.Range;
                float near = lightRange * 0.001f;
                float far = lightRange;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(1, 0, 0), new Vector3(0, -1, 0), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(-1, 0, 0), new Vector3(0, 1, 0), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(0, 1, 0), new Vector3(0, 0, -1), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(0, -1, 0), new Vector3(0, 0, 1), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(0, 0, 1), new Vector3(-1, 0, 0), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;

                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, new Vector3(0, 0, -1), new Vector3(1, 0, 0), near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderWrap);
                index++;
            }
        }

        static int SplitTest(int v)
        {
            int pointLightSplit = 2;
            for (int i = 4; i * i < v; i += 2)
                pointLightSplit = i;
            pointLightSplit *= 2;
            return pointLightSplit;
        }

        static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
        {
            { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
            { DebugRenderType.AO,"DEBUG_AO"},
            { DebugRenderType.Bitangent,"DEBUG_BITANGENT"},
            { DebugRenderType.Depth,"DEBUG_DEPTH"},
            { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
            { DebugRenderType.DiffuseProbes,"DEBUG_DIFFUSE_PROBES"},
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
