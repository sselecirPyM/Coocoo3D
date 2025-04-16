using Arch.Core;
using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using Coocoo3DGraphics.Commanding;
using RenderPipelines.LambdaPipe;
using RenderPipelines.LambdaRenderers;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace RenderPipelines;

public partial class ForwardRenderPipeline
{
    struct LightingData
    {
        public Vector3 LightDir;
        public uint LightType;
        public Vector3 LightColor;
        public float useless;
    }

    void BindCBVFinal(CBVProxy cbv)
    {
        cbv.Set("g_mWorldToProj", Matrix4x4.Transpose(ViewProjection));
        cbv.Set("g_mProjToWorld", Matrix4x4.Transpose(InvertViewProjection));
        cbv.Set("g_mWorldToView", Matrix4x4.Transpose(View));
        cbv.Set("g_cameraFarClip", Far);
        cbv.Set("g_cameraNearClip", Near);
        cbv.Set("g_cameraFOV", Fov);
        cbv.Set("g_cameraAspectRatio", AspectRatio);
        cbv.Set("g_camPos", CameraPosition);
        cbv.Set("g_skyBoxMultiple", SkyLightMultiple);
        cbv.Set("_fogColor", FogColor);
        cbv.Set("_fogDensity", FogDensity);
        cbv.Set("_startDistance", FogStartDistance);
        cbv.Set("_endDistance", FogEndDistance);
        cbv.Set("_widthHeight", OutputSize);
        cbv.Set("g_Brightness", Brightness);
        cbv.Set("_volumeLightIterCount", VolumetricLightingSampleCount);
        cbv.Set("_volumeLightMaxDistance", VolumetricLightingDistance);
        cbv.Set("_volumeLightIntensity", VolumetricLightingIntensity);
        cbv.Set("ShadowMapVP", [Matrix4x4.Transpose(ShadowMapVP), Matrix4x4.Transpose(ShadowMapVP1)]);
        cbv.Set("Lightings", new LightingData { LightColor = LightColor, LightDir = LightDir });
        cbv.Set("g_GIVolumePosition", GIVolumePosition);
        cbv.Set("g_AODistance", AODistance);
        cbv.Set("g_GIVolumeSize", GIVolumeSize);
        cbv.Set("g_AOLimit", AOLimit);
        cbv.Set("g_AORaySampleCount", AORaySampleCount);
        cbv.Set("g_RandomI", RandomI);
        cbv.Set("g_lightMapSplit", Split);
        cbv.Set("g_camLeft", CameraLeft);
        cbv.Set("g_camDown", CameraDown);
        cbv.Set("g_RayTracingReflectionQuality", RayTracingReflectionQuality);
        cbv.Set("g_RayTracingReflectionThreshold", RayTracingReflectionThreshold);
    }

    public Matrix4x4 ViewProjection;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Matrix4x4 InvertViewProjection;
    public Vector3 CameraPosition;
    public Vector3 CameraLeft;
    public Vector3 CameraDown;
    public Vector3 CameraBack;


    [UIDragFloat(0.01f, 0, name: "亮度")]
    public float Brightness = 1;

    [UIDragFloat(0.01f, 0, name: "天空盒亮度")]
    public float SkyLightMultiple = 3;

    [UIShow(name: "启用雾")]
    public bool EnableFog;
    [UIColor(name: "雾颜色")]
    public Vector3 FogColor = new Vector3(0.4f, 0.4f, 0.6f);
    [UIDragFloat(0.001f, 0, name: "雾密度")]
    public float FogDensity = 0.005f;
    [UIDragFloat(0.1f, 0, name: "雾开始距离")]
    public float FogStartDistance = 5;
    //[UIDragFloat(0.1f, 0, name: "雾结束距离")]
    public float FogEndDistance = 100000;


    [UIShow(name: "启用TAA抗锯齿")]
    public bool EnableTAA;

    [UIDragFloat(0.01f, name: "TAA混合系数")]
    public float TAAFactor = 0.3f;

    public float Far;
    public float Near;
    public float Fov;
    public float AspectRatio;


    public Matrix4x4 ShadowMapVP;
    public Matrix4x4 ShadowMapVP1;

    public Vector3 LightDir;
    public Vector3 LightColor;
    public int Split;


    [UIShow(name: "延迟渲染")]
    public bool DeferredRendering;
    #region deferred

    Texture2D gbuffer0 { get; set; }
    Texture2D gbuffer1 { get; set; }
    Texture2D gbuffer2 { get; set; }
    Texture2D gbuffer3 { get; set; }

    [Size(2048, 2048, 9)]
    [Format(ResourceFormat.R32G32_Float)]
    [AutoClear]
    public Texture2D _HiZBuffer
    {
        get => x__HiZBuffer;
        set
        {
            x__HiZBuffer = value;
        }
    }
    Texture2D x__HiZBuffer;

    GPUBuffer GIBuffer;
    GPUBuffer GIBufferWrite;

    [UIShow(name: "启用体积光")]
    public bool EnableVolumetricLighting;

    [UIDragInt(1, 1, 256, name: "体积光采样次数")]
    public int VolumetricLightingSampleCount = 16;

    [UIDragFloat(0.1f, name: "体积光距离")]
    public float VolumetricLightingDistance = 12;

    [UIDragFloat(0.1f, name: "体积光强度")]
    public float VolumetricLightingIntensity = 0.001f;

    [UIShow(name: "启用SSAO")]
    public bool EnableSSAO;

    [UIDragFloat(0.1f, 0, name: "AO距离")]
    public float AODistance = 1;

    [UIDragFloat(0.01f, 0.1f, name: "AO限制")]
    public float AOLimit = 0.3f;

    [UIDragInt(1, 0, 128, name: "AO光线采样次数")]
    public int AORaySampleCount = 32;

    [UIShow(name: "启用屏幕空间反射")]
    public bool EnableSSR;


    [UIShow(name: "启用光线追踪")]
    public bool EnableRayTracing;

    [UIDragFloat(0.01f, 0, 5, name: "光线追踪反射质量")]
    public float RayTracingReflectionQuality = 1.0f;

    [UIDragFloat(0.01f, 0, 1.0f, name: "光线追踪反射阈值")]
    public float RayTracingReflectionThreshold = 0.5f;

    [UIShow(name: "更新全局光照")]
    public bool UpdateGI;

    [UIDragFloat(1.0f, name: "全局光照位置")]
    public Vector3 GIVolumePosition = new Vector3(0, 2.5f, 0);

    [UIDragFloat(1.0f, name: "全局光照范围")]
    public Vector3 GIVolumeSize = new Vector3(20, 5, 20);

    [UIShow(name: "使用全局光照")]
    public bool UseGI;
    [UIShow(name: "无背景")]
    public bool NoBackGround;

    public (int, int) OutputSize;
    public int RandomI;
    #endregion


    [UIShow(name: "启用泛光")]
    public bool EnableBloom;

    [UIDragFloat(0.01f, name: "泛光阈值")]
    public float BloomThreshold = 1.05f;
    [UIDragFloat(0.01f, name: "泛光强度")]
    public float BloomIntensity = 0.1f;

    public List<PointLightData> pointLights = new List<PointLightData>();

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


    public void Config(SuperPipelineConfig s, PipelineContext c)
    {


        var q = new QueryDescription().WithAll<VisualComponent>();
        var world = renderPipelineView.rpc.scene.world;
        List<VisualComponent> visualComponents = new List<VisualComponent>();
        world.Query(q, (Entity entity, ref VisualComponent visualComponent, ref Transform transform) =>
        {
            visualComponent.transform = transform;
            visualComponents.Add(visualComponent);
        });
        var q2 = new QueryDescription().WithAll<MMDRendererComponent, Transform>();
        world.Query(q2, (Entity entity, ref MMDRendererComponent renderer, ref Transform transform) =>
        {
            renderer.SetTransform(transform);
        });



        var camera = this.renderPipelineView.cameraData;
        this.camera = camera;
        if (EnableTAA)
        {
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputWidth, (float)(random.NextDouble() * 2 - 1) / outputHeight);
            camera = camera.GetJitter(jitterVector);
        }
        c.ConfigRenderer<TAAConfig>(s =>
        {
            s.EnableTAA = EnableTAA;
            s.target = noPostProcess;
            s.depth = depth;
            s.history = noPostProcess2;
            s.historyDepth = depth2;
            s.DebugRenderType = DebugRenderType;
            s.camera = this.camera;
            s.historyCamera = historyCamera;
        });

        this.SetCamera(camera);

        BoundingFrustum frustum = new BoundingFrustum(ViewProjection);


        pointLights.Clear();

        DirectionalLightData directionalLight = null;
        foreach (var visual in visualComponents)
        {
            var material = visual.material;
            if (visual.material.Type != UIShowType.Light)
                continue;
            var lightMaterial = DictExt.ConvertToObject<LightMaterial>(material.Parameters);
            var lightType = lightMaterial.LightType;
            if (lightType == LightType.Directional)
            {
                if (directionalLight != null)
                    continue;
                directionalLight = new DirectionalLightData()
                {
                    Color = lightMaterial.LightColor,
                    Direction = Vector3.Transform(-Vector3.UnitZ, visual.transform.rotation),
                    Rotation = visual.transform.rotation
                };
            }
            else if (lightType == LightType.Point)
            {
                if (pointLights.Count >= 4)
                {
                    continue;
                }
                float range = lightMaterial.LightRange;
                if (!frustum.Intersects(new BoundingSphere(visual.transform.position, range)))
                    continue;
                pointLights.Add(new PointLightData()
                {
                    Color = lightMaterial.LightColor,
                    Position = visual.transform.position,
                    Range = range,
                });
            }
        }

        if (directionalLight != null)
        {
            var dl = directionalLight;
            ShadowMapVP = dl.GetLightingMatrix(InvertViewProjection, ShadowNearDistance, ShadowMidDistance);
            ShadowMapVP1 = dl.GetLightingMatrix(InvertViewProjection, ShadowMidDistance, ShadowFarDistance);
            LightDir = dl.Direction;
            LightColor = dl.Color;
        }
        else
        {
            ShadowMapVP = Matrix4x4.Identity;
            ShadowMapVP1 = Matrix4x4.Identity;
            LightDir = Vector3.UnitZ;
            LightColor = Vector3.Zero;
        }

        Split = ShadowSize(pointLights.Count * 6);

        c.ConfigRenderer<ShadowRenderConfig>(s =>
        {
            s.viewports.Clear();
            s.renderables.Clear();
            s.renderables.AddRange(renderHelper.Renderables);

            if (directionalLight != null)
            {
                s.viewports.Add(new RenderDepthToViewport()
                {
                    viewProjection = ShadowMapVP,
                    RenderTarget = _ShadowMap,
                    Rectangle = new Rectangle(0, 0, _ShadowMap.width / 2, _ShadowMap.height / 2)
                });
                s.viewports.Add(new RenderDepthToViewport()
                {
                    viewProjection = ShadowMapVP1,
                    RenderTarget = _ShadowMap,
                    Rectangle = new Rectangle(_ShadowMap.width / 2, 0, _ShadowMap.width / 2, _ShadowMap.height / 2)
                });
            }

            if (pointLights.Count > 0)
            {
                int index = 0;
                int width = _ShadowMap.width;
                int height = _ShadowMap.height;
                foreach (var pl in pointLights)
                {
                    var lightRange = pl.Range;
                    float near = lightRange * 0.001f;
                    float far = lightRange;

                    foreach (var val in directions)
                    {
                        s.viewports.Add(new RenderDepthToViewport()
                        {
                            viewProjection = GetShadowMapMatrix(pl.Position, val.Item1, val.Item2, near, far),
                            RenderTarget = _ShadowMap,
                            Rectangle = GetViewportScissorRectangle(index, Split, width, height)
                        });
                        index++;
                    }
                }
            }
        });


        void SetDrawObjectConfig(DrawObjectConfig s)
        {
            s.keywords.Clear();
            s.keywords2.Clear();
            s.additionalSRV.Clear();
            if (directionalLight != null)
                s.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
            if (pointLights.Count > 0)
            {
                var pointLightDatas = CollectionsMarshal.AsSpan(pointLights);
                var rawData = MemoryMarshal.AsBytes(pointLightDatas).ToArray();

                s.additionalSRV[11] = rawData;
                s.keywords.Add(("POINT_LIGHT_COUNT", pointLights.Count.ToString()));
            }
            if (debugKeywords.TryGetValue(DebugRenderType, out string debugKeyword))
            {
                s.keywords.Add((debugKeyword, "1"));
            }
            if (UseGI)
            {
                s.keywords.Add(("ENABLE_GI", "1"));
                s.additionalSRV[9] = GIBuffer;
            }
            if (EnableFog)
                s.keywords.Add(("ENABLE_FOG", "1"));

            s.shader = "ForwardRender.hlsl";
            s._ShadowMap = _ShadowMap;
            s._BRDFLUT = _BRDFLUT;
            s._Environment = _Environment;
            s.RenderTargets = [noPostProcess];
            s.DepthStencil = depth;
            s.psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            };
            s.Binding = (ct) =>
            {
                ct.SetCBV(2, BindCBVFinal);
            };
        }

        if (DeferredRendering)
        {
            OutputSize = (noPostProcess.width, noPostProcess.height);

            var pipelineMaterial = new PipelineMaterial()
            {
                gbuffer0 = gbuffer0,
                gbuffer1 = gbuffer1,
                gbuffer2 = gbuffer2,
                gbuffer3 = gbuffer3,
                _SkyBox = _SkyBox,
                depth = depth,
                depth2 = depth2,
                GIBuffer = GIBuffer,
                GIBufferWrite = GIBufferWrite,
                _BRDFLUT = _BRDFLUT,
                _Environment = _Environment,
                _HiZBuffer = _HiZBuffer,
                _ShadowMap = _ShadowMap,
            };
            var random = Random.Shared;
            RandomI = random.Next();


            c.ConfigRenderer<DrawGBufferConfig>(s =>
            {
                s.CameraLeft = CameraLeft;
                s.CameraDown = CameraDown;
                s.viewProjection = ViewProjection;
                s.RenderTargets = [gbuffer0, gbuffer1, gbuffer2, gbuffer3];
                s.DepthStencil = depth;
            });
            c.ConfigRenderer<DrawDecalConfig>(s =>
            {
                s.RenderTargets = [gbuffer0, gbuffer2];
                s.depthStencil = depth;
                s.ViewProjection = ViewProjection;
                s.Visuals = visualComponents;
            });
            c.ConfigRenderer<HizConfig>(s =>
            {
                s.Enable = EnableSSR;
                s.input = depth;
                s.output = _HiZBuffer;
            });
            c.ConfigRenderer<DeferredShadingConfig>(s =>
            {
                s.keywords.Clear();
                if (directionalLight != null)
                {
                    s.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
                    if (EnableVolumetricLighting)
                    {
                        s.keywords.Add(("ENABLE_VOLUME_LIGHTING", "1"));
                    }
                }
                if (pointLights.Count > 0)
                {
                    s.keywords.Add(("POINT_LIGHT_COUNT", pointLights.Count.ToString()));
                    s.pointLightDatas.Clear();
                    s.pointLightDatas.AddRange(pointLights);
                }
                if (EnableRayTracing)
                {
                    s.keywords.Add(("RAY_TRACING", "1"));
                }
                if (debugKeywords.TryGetValue(DebugRenderType, out string debugKeyword))
                {
                    s.keywords.Add((debugKeyword, "1"));
                }
                s.EnableSSR = EnableSSR;
                s.EnableSSAO = EnableSSAO;
                s.EnableFog = EnableFog;
                s.UseGI = UseGI;
                s.NoBackGround = NoBackGround;
                s.pipelineMaterial = pipelineMaterial;
                s.RenderTarget = noPostProcess;

                s.Binding = (ct) =>
                {
                    ct.SetCBV(0, BindCBVFinal);
                };
            });

            c.ConfigRenderer<RayTracingConfig>(s =>
            {
                s.RayTracing = EnableRayTracing;
                s.RayTracingGI = UpdateGI;
                if (!s.RayTracing && !s.RayTracingGI)
                    return;
                s.UseGI = UseGI;
                s.pipelineMaterial = pipelineMaterial;
                s.renderTarget = gbuffer2;
                s.directionalLight = directionalLight;
                s.afterGI = () =>
                {
                    SwapBuffer("GIBuffer", "GIBufferWrite");
                };
                s.Binding = (ct) =>
                {
                    ct.SetCBV(0, BindCBVFinal);
                };
            });

            c.ConfigRendererKeyed<DrawObjectConfig>("transparent", s =>
            {
                SetDrawObjectConfig(s);
                s.DrawOpaque = false;
                s.DrawTransparent = true;
                s.psoDesc.blendState = BlendState.Alpha;
            });
        }
        else
        {
            c.ConfigRenderer<SkyboxRenderConfig>(s =>
            {
                s.skybox = _SkyBox;
                s.RenderTarget = noPostProcess;
                s.SkyLightMultiple = SkyLightMultiple * Brightness;
                s.camera = this.camera;
            });
            c.ConfigRendererKeyed<DrawObjectConfig>("opaque", s =>
            {
                SetDrawObjectConfig(s);
                s.DrawOpaque = true;
                s.DrawTransparent = false;
            });
            c.ConfigRendererKeyed<DrawObjectConfig>("transparent", s =>
            {
                SetDrawObjectConfig(s);
                s.DrawOpaque = false;
                s.DrawTransparent = true;
                s.psoDesc.blendState = BlendState.Alpha;
            });
        }

        c.ConfigRenderer<PostProcessingConfig>(s =>
        {
            s.EnableBloom = EnableBloom;
            s.BloomIntensity = BloomIntensity;
            s.BloomThreshold = BloomThreshold;

            s.inputColor = noPostProcess;
            s.output = output;
            s.intermedia1 = intermedia1;
            s.intermedia2 = intermedia2;
            s.intermedia3 = intermedia3;

        });
    }
    public void Execute(SuperPipelineConfig s, PipelineContext c)
    {
        c.Execute<ShadowRenderConfig>();

        if (DeferredRendering)
        {
            c.Execute<DrawGBufferConfig>();
            c.Execute<DrawDecalConfig>();
            c.Execute<HizConfig>();
            c.Execute<RayTracingConfig>();
            c.Execute<DeferredShadingConfig>();
            c.ExecuteKeyed<DrawObjectConfig>("transparent");
        }
        else
        {
            c.Execute<SkyboxRenderConfig>();
            c.ExecuteKeyed<DrawObjectConfig>("opaque");
            c.ExecuteKeyed<DrawObjectConfig>("transparent");
        }

        c.Execute<TAAConfig>();
        c.Execute<PostProcessingConfig>();
        historyCamera = this.camera;
    }

    static Matrix4x4 GetShadowMapMatrix(Vector3 pos, Vector3 dir, Vector3 up, float near, float far)
    {
        return Matrix4x4.CreateLookAt(pos, pos + dir, up)
         * Matrix4x4.CreatePerspectiveFieldOfView(1.57079632679f, 1, near, far);
    }

    static Rectangle GetViewportScissorRectangle(int index, int split, int width, int height)
    {
        float xOffset = (float)(index % split) / split;
        float yOffset = (float)(index / split) / split;
        float size = 1.0f / split;

        int x = (int)(width * xOffset);
        int y = (int)(height * (yOffset + 0.5f));
        int sizeX1 = (int)(width * size);
        int sizeY1 = (int)(height * size);

        var rect = new Rectangle(x, y, sizeX1, sizeY1);
        return rect;
    }

    static readonly (Vector3, Vector3)[] directions =
    {
        (new Vector3(1, 0, 0), new Vector3(0, -1, 0)),
        (new Vector3(-1, 0, 0), new Vector3(0, 1, 0)),
        (new Vector3(0, 1, 0), new Vector3(0, 0, -1)),
        (new Vector3(0, -1, 0), new Vector3(0, 0, 1)),
        (new Vector3(0, 0, 1), new Vector3(-1, 0, 0)),
        (new Vector3(0, 0, -1), new Vector3(1, 0, 0))
    };

    static int ShadowSize(int v)
    {
        v *= 2;
        int pointLightSplit = 2;
        for (int i = 4; i * i < v; i += 2)
            pointLightSplit = i;
        pointLightSplit += 2;
        return pointLightSplit;
    }

    static readonly Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
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
