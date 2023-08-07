using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

namespace RenderPipelines;

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
            "_Spa",
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
            nameof(CameraLeft),
            nameof(CameraDown),
        },
        AutoKeyMap =
        {
            ("UseNormalMap","USE_NORMAL_MAP"),
            ("UseSpa","USE_SPA"),
        },
        filter = FilterOpaque,
    };
    public DrawDecalPass decalPass = new DrawDecalPass()
    {
        renderTargets = new string[]
        {
            "gbuffer0",
            "gbuffer2",
        },
        srvs = new string[]
        {
            null,
            "DecalColorTexture",
            "DecalEmissiveTexture",
        },
        CBVPerObject = new object[]
        {
            null,
            null,
            "_DecalEmissivePower"
        },
        AutoKeyMap =
        {
            ("EnableDecalColor","ENABLE_DECAL_COLOR"),
            ("EnableDecalEmissive","ENABLE_DECAL_EMISSIVE"),
        }
    };
    public FinalPass finalPass = new FinalPass()
    {
        renderTargets = new string[1],
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
            "_HiZBuffer",
            "GIBuffer",
        },
        cbvs = new object[][]
        {
            new object []
            {
                nameof(ViewProjection),
                nameof(InvertViewProjection),
                nameof(View),
                nameof(Projection),
                nameof(Far),
                nameof(Near),
                nameof(Fov),
                nameof(AspectRatio),
                nameof(CameraPosition),
                nameof(SkyLightMultiple),
                nameof(FogColor),
                nameof(FogDensity),
                nameof(FogStartDistance),
                nameof(FogEndDistance),
                nameof(OutputSize),
                nameof(Brightness),
                nameof(VolumetricLightingSampleCount),
                nameof(VolumetricLightingDistance),
                nameof(VolumetricLightingIntensity),
                nameof(ShadowMapVP),
                nameof(ShadowMapVP1),
                nameof(LightDir),
                0,
                nameof(LightColor),
                0,
                nameof(GIVolumePosition),
                nameof(AODistance),
                nameof(GIVolumeSize),
                nameof(AOLimit),
                nameof(AORaySampleCount),
                nameof(RandomI),
                nameof(Split),
            },
            new object[]
            {
                null
            }
        },
    };

    public DrawObjectPass drawObjectTransparent = new DrawObjectPass()
    {
        shader = "ForwardRender.hlsl",
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
            "_Spa",
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
            nameof(SkyLightMultiple),
            nameof(FogColor),
            nameof(FogDensity),
            nameof(FogStartDistance),
            nameof(FogEndDistance),
            nameof(CameraLeft),
            nameof(CameraDown),
            nameof(Split),
            nameof(GIVolumePosition),
            nameof(GIVolumeSize),
        },
        AutoKeyMap =
        {
            (nameof(EnableFog),"ENABLE_FOG"),
            ("UseNormalMap","USE_NORMAL_MAP"),
            ("UseSpa","USE_SPA"),
            (nameof(UseGI),"ENABLE_GI"),
        },
        filter = FilterTransparent,
    };

    public RayTracingPass rayTracingPass = new RayTracingPass()
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
    };

    public HiZPass HiZPass = new HiZPass()
    {
        output = "_HiZBuffer"
    };

    public Random random = new Random(0);

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


    [UIDragFloat(0.01f, 0, name: "亮度"), Indexable]
    public float Brightness = 1;

    [UIDragFloat(0.01f, 0, name: "天空盒亮度"), Indexable]
    public float SkyLightMultiple = 3;


    [UIShow(name: "启用体积光"), Indexable]
    public bool EnableVolumetricLighting;

    [UIDragInt(1, 1, 256, name: "体积光采样次数"), Indexable]
    public int VolumetricLightingSampleCount = 16;

    [UIDragFloat(0.1f, name: "体积光距离"), Indexable]
    public float VolumetricLightingDistance = 12;

    [UIDragFloat(0.1f, name: "体积光强度"), Indexable]
    public float VolumetricLightingIntensity = 0.001f;

    [UIShow(name: "启用SSAO"), Indexable]
    public bool EnableSSAO;

    [UIDragFloat(0.1f, 0, name: "AO距离"), Indexable]
    public float AODistance = 1;

    [UIDragFloat(0.01f, 0.1f, name: "AO限制"), Indexable]
    public float AOLimit = 0.3f;

    [UIDragInt(1, 0, 128, name: "AO光线采样次数"), Indexable]
    public int AORaySampleCount = 32;

    [UIShow(name: "启用屏幕空间反射"), Indexable]
    public bool EnableSSR;


    [UIShow(name: "启用光线追踪")]
    public bool EnableRayTracing;

    [UIDragFloat(0.01f, 0, 5, name: "光线追踪反射质量"), Indexable]
    public float RayTracingReflectionQuality = 1.0f;

    [UIDragFloat(0.01f, 0, 1.0f, name: "光线追踪反射阈值"), Indexable]
    public float RayTracingReflectionThreshold = 0.5f;

    [UIShow(name: "更新全局光照")]
    public bool UpdateGI;

    [UIDragFloat(1.0f, name: "全局光照位置"), Indexable]
    public Vector3 GIVolumePosition = new Vector3(0, 2.5f, 0);

    [UIDragFloat(1.0f, name: "全局光照范围"), Indexable]
    public Vector3 GIVolumeSize = new Vector3(20, 5, 20);

    [UIShow(name: "使用全局光照"), Indexable]
    public bool UseGI;

    [UIShow(name: "启用雾"), Indexable]
    public bool EnableFog;
    [UIColor(name: "雾颜色"), Indexable]
    public Vector3 FogColor = new Vector3(0.4f, 0.4f, 0.6f);
    [UIDragFloat(0.001f, 0, name: "雾密度"), Indexable]
    public float FogDensity = 0.005f;
    [UIDragFloat(0.1f, 0, name: "雾开始距离"), Indexable]
    public float FogStartDistance = 5;
    //[UIDragFloat(0.1f, 0, name: "雾结束距离")]
    [Indexable]
    public float FogEndDistance = 100000;
    [UIShow(name: "无背景"), Indexable]
    public bool NoBackGround;

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
    public (int, int) OutputSize;

    [Indexable]
    public int RandomI;

    public string renderTarget;
    public string depthStencil;

    [Indexable]
    public int Split;

    public DebugRenderType DebugRenderType;

    public IEnumerable<VisualComponent> Visuals;

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
        decalPass.viewProj = ViewProjection;


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

    public void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;
        drawGBuffer.depthStencil = depthStencil;
        HiZPass.input = depthStencil;
        drawObjectTransparent.depthStencil = depthStencil;
        drawObjectTransparent.renderTargets[0] = renderTarget;
        decalPass.srvs[0] = depthStencil;
        decalPass.Visuals = Visuals;
        finalPass.renderTargets[0] = renderTarget;
        finalPass.srvs[5] = depthStencil;

        rayTracingPass.RenderTarget = "gbuffer2";
        rayTracingPass.srvs[3] = depthStencil;

        RandomI = random.Next();

        BoundingFrustum frustum = new BoundingFrustum(ViewProjection);

        int pointLightCount = 0;
        byte[] pointLightData = ArrayPool<byte>.Shared.Rent(64 * 32);
        DirectionalLightData directionalLight = null;
        var pointLightWriter = SpanWriter.New<PointLightData>(pointLightData);
        foreach (var visual in Visuals)
        {
            var material = visual.material;
            if (visual.UIShowType != Caprice.Display.UIShowType.Light)
                continue;
            var lightType = (LightType)renderHelper.GetIndexableValue("LightType", material);
            if (lightType == LightType.Directional)
            {
                if (directionalLight != null)
                    continue;
                directionalLight = new DirectionalLightData()
                {
                    Color = (Vector3)renderHelper.GetIndexableValue("LightColor", material),
                    Direction = Vector3.Transform(-Vector3.UnitZ, visual.transform.rotation),
                    Rotation = visual.transform.rotation
                };
            }
            else if (lightType == LightType.Point)
            {
                if (pointLightCount >= 64)
                    continue;
                float range = (float)renderHelper.GetIndexableValue("LightRange", material);
                if (!frustum.Intersects(new BoundingSphere(visual.transform.position, range)))
                    continue;
                pointLightWriter.Write(new PointLightData()
                {
                    Color = (Vector3)renderHelper.GetIndexableValue("LightColor", material),
                    Position = visual.transform.position,
                    Range = range,
                });
                pointLightCount++;
            }
        }

        if (directionalLight != null)
        {
            var dl = directionalLight;
            ShadowMapVP = dl.GetLightingMatrix(InvertViewProjection, 0, 0.977f);
            ShadowMapVP1 = dl.GetLightingMatrix(InvertViewProjection, 0.977f, 0.993f);
            LightDir = dl.Direction;
            LightColor = dl.Color;
            finalPass.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
            drawObjectTransparent.keywords.Add(("ENABLE_DIRECTIONAL_LIGHT", "1"));
            if (EnableVolumetricLighting)
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
        if (EnableRayTracing)
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

        renderHelper.PushParameters(this);
        if (directionalLight != null)
        {
            renderWrap.SetRenderTarget(null, "_ShadowMap", false, true);
            var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
            int width = shadowMap.width;
            int height = shadowMap.height;
            drawShadowMap.CBVPerObject[1] = ShadowMapVP;
            drawShadowMap.scissorViewport = new Rectangle(0, 0, width / 2, height / 2);
            drawShadowMap.Execute(renderHelper);
            drawShadowMap.CBVPerObject[1] = ShadowMapVP1;
            drawShadowMap.scissorViewport = new Rectangle(width / 2, 0, width / 2, height / 2);
            drawShadowMap.Execute(renderHelper);
        }

        Split = SplitTest(pointLightCount * 6);
        if (pointLightCount > 0)
        {
            var pointLightDatas = MemoryMarshal.Cast<byte, PointLightData>(pointLightData).Slice(0, pointLightCount);
            DrawPointShadow(renderHelper, pointLightDatas);

            finalPass.cbvs[1][0] = (pointLightData, pointLightCount * 32);
            finalPass.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
            finalPass.keywords.Add(("POINT_LIGHT_COUNT", pointLightCount.ToString()));

            drawObjectTransparent.CBVPerObject[1] = (pointLightData, pointLightCount * 32);
            drawObjectTransparent.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
            drawObjectTransparent.keywords.Add(("POINT_LIGHT_COUNT", pointLightCount.ToString()));
        }
        drawGBuffer.Execute(renderHelper);
        decalPass.Execute(renderHelper);
        if (EnableSSR)
            HiZPass.Execute(renderHelper);

        if (EnableRayTracing || UpdateGI)
        {
            rayTracingPass.RayTracing = EnableRayTracing;
            rayTracingPass.RayTracingGI = UpdateGI;
            rayTracingPass.UseGI = UseGI;
            rayTracingPass.Execute(renderHelper, directionalLight);
        }
        finalPass.EnableSSR = EnableSSR;
        finalPass.EnableSSAO = EnableSSAO;
        finalPass.EnableFog = EnableFog;
        finalPass.UseGI = UseGI;
        finalPass.NoBackGround = NoBackGround;
        finalPass.Execute(renderHelper);
        drawObjectTransparent.Execute(renderHelper);

        renderHelper.PopParameters();

        finalPass.cbvs[1][0] = null;
        drawObjectTransparent.CBVPerObject[1] = null;

        ArrayPool<byte>.Shared.Return(pointLightData);


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

    static readonly (Vector3, Vector3)[] table =
    {
        (new Vector3(1, 0, 0), new Vector3(0, -1, 0)),
        (new Vector3(-1, 0, 0), new Vector3(0, 1, 0)),
        (new Vector3(0, 1, 0), new Vector3(0, 0, -1)),
        (new Vector3(0, -1, 0), new Vector3(0, 0, 1)),
        (new Vector3(0, 0, 1), new Vector3(-1, 0, 0)),
        (new Vector3(0, 0, -1), new Vector3(1, 0, 0))
    };
    void DrawPointShadow(RenderHelper renderHelper, Span<PointLightData> pointLightDatas)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;
        int index = 0;
        var shadowMap = renderWrap.GetRenderTexture2D("_ShadowMap");
        int width = shadowMap.width;
        int height = shadowMap.height;
        foreach (var pl in pointLightDatas)
        {
            var lightRange = pl.Range;
            float near = lightRange * 0.001f;
            float far = lightRange;

            foreach (var val in table)
            {
                drawShadowMap.CBVPerObject[1] = GetShadowMapMatrix(pl.Position, val.Item1, val.Item2, near, far);
                drawShadowMap.scissorViewport = GetRectangle(index, Split, width, height);
                drawShadowMap.Execute(renderHelper);
                index++;
            }
        }
    }

    static int SplitTest(int v)
    {
        v *= 2;
        int pointLightSplit = 2;
        for (int i = 4; i * i < v; i += 2)
            pointLightSplit = i;
        pointLightSplit += 2;
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

    static bool FilterOpaque(RenderHelper renderHelper, MeshRenderable renderable, List<(string, string)> keywords)
    {
        if (true.Equals(renderHelper.GetIndexableValue("IsTransparent", renderable.material)))
        {
            return false;
        }
        return true;
    }

    static bool FilterTransparent(RenderHelper renderHelper, MeshRenderable renderable, List<(string, string)> keywords)
    {
        if (true.Equals(renderHelper.GetIndexableValue("IsTransparent", renderable.material)))
        {
            return true;
        }
        return false;
    }
}
