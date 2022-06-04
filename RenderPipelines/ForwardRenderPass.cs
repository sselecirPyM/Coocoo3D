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
using Vortice.Mathematics;

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
                "_Spa",
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
                nameof(CameraLeft),
                nameof(CameraDown),
                nameof(Split),
            },
            AutoKeyMap =
            {
                ("EnableFog","ENABLE_FOG"),
                ("UseNormalMap","USE_NORMAL_MAP"),
                ("UseSpa","USE_SPA"),
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
        public Vector3 CameraLeft;
        [Indexable]
        public Vector3 CameraDown;
        [Indexable]
        public Vector3 CameraBack;

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

            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-camera.Angle.Y, -camera.Angle.X, -camera.Angle.Z);
            CameraLeft = Vector3.Transform(-Vector3.UnitX, rotateMatrix);
            CameraDown = Vector3.Transform(-Vector3.UnitY, rotateMatrix);
            CameraBack = Vector3.Transform(-Vector3.UnitZ, rotateMatrix);
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
            //var dls = renderWrap.directionalLights;
            //var pls = renderWrap.pointLights;

            BoundingFrustum frustum = new BoundingFrustum(ViewProjection);

            int pointLightCount = 0;
            byte[] pointLightData = ArrayPool<byte>.Shared.Rent(64 * 32);
            DirectionalLightData? directionalLight = null;
            var pointLightWriter = new SpanWriter<PointLightData>(MemoryMarshal.Cast<byte, PointLightData>(pointLightData));
            for (int i = 0; i < renderWrap.visuals.Count; i++)
            {
                var visual = renderWrap.visuals[i];
                var mat = visual.material;
                if (visual.UIShowType == Caprice.Display.UIShowType.Light)
                {
                    var lightType = (LightType)renderWrap.GetIndexableValue("LightType", mat);
                    if (lightType == LightType.Directional)
                    {
                        if (directionalLight != null)
                            continue;
                        directionalLight = new DirectionalLightData()
                        {
                            Color = (Vector3)renderWrap.GetIndexableValue("LightColor", mat),
                            Direction = Vector3.Transform(-Vector3.UnitZ, visual.transform.rotation),
                            Rotation = visual.transform.rotation
                        };
                    }
                    else if (lightType == LightType.Point)
                    {
                        if (pointLightCount >= 64) continue;
                        float range = (float)renderWrap.GetIndexableValue("LightRange", mat);
                        if (frustum.Intersects(new BoundingSphere(visual.transform.position, range)))
                        {
                            pointLightWriter.Write(new PointLightData()
                            {
                                Color = (Vector3)renderWrap.GetIndexableValue("LightColor", mat),
                                Position = visual.transform.position,
                                Range = range,
                            });
                            pointLightCount++;
                        }
                    }
                }
            }

            if (directionalLight != null)
            {
                var dl = directionalLight.Value;
                ShadowMapVP = dl.GetLightingMatrix(InvertViewProjection, 0, 0.977f);
                ShadowMapVP1 = dl.GetLightingMatrix(InvertViewProjection, 0.977f, 0.993f);
                LightDir = dl.Direction;
                LightColor = dl.Color;
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
            if (directionalLight != null)
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
            Split = SplitTest(pointLightCount * 12);

            if (pointLightCount > 0)
            {
                var pointLightDatas = MemoryMarshal.Cast<byte, PointLightData>(pointLightData).Slice(0, pointLightCount);
                DrawPointShadow(renderWrap, pointLightDatas);

                drawObject.CBVPerObject[1] = (pointLightData, pointLightCount * 32);
                drawObject.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
                drawObject.keywords.Add(("POINT_LIGHT_COUNT", pointLightCount.ToString()));
            }

            if (debugKeywords.TryGetValue(DebugRenderType, out string debugKeyword))
            {
                drawObject.keywords.Add((debugKeyword, "1"));
            }

            drawSkyBox.Execute(renderWrap);

            drawObject.psoDesc.blendState = BlendState.None;
            drawObject.filter = FilterOpaque;
            drawObject.Execute(renderWrap);

            drawObject.psoDesc.blendState = BlendState.Alpha;
            drawObject.filter = FilterTransparent;
            drawObject.Execute(renderWrap);

            renderWrap.PopParameters();

            ArrayPool<byte>.Shared.Return(pointLightData);

            drawObject.keywords.Clear();
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

        void DrawPointShadow(RenderWrap renderWrap, Span<PointLightData> pointLightDatas)
        {
            int index = 0;
            var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
            int width = shadowMap.width;
            int height = shadowMap.height;
            foreach (var pl in pointLightDatas)
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
    }
}
