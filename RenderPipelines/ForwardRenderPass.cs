using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
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
    public DrawSkyBoxPass drawSkyBox = new DrawSkyBoxPass();

    public DrawShadowMap drawShadowMap = new DrawShadowMap();

    public DrawObjectPass drawObject = new DrawObjectPass()
    {
        shader = "ForwardRender.hlsl",
        psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
        },
        srvs = new string[]
        {
            nameof(_Albedo),
            nameof(_Metallic),
            nameof(_Roughness),
            nameof(_Emissive),
            nameof(_ShadowMap),
            nameof(_Environment),
            nameof(_BRDFLUT),
            nameof(_Normal),
            nameof(_Spa),
        },
        CBVPerObject = new object[]
        {
            null,
            null,
            nameof(Metallic),
            nameof(Roughness),
            nameof(Emissive),
            nameof(Specular),
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
        },
        AutoKeyMap =
        {
            (nameof(EnableFog),"ENABLE_FOG"),
            (nameof(UseNormalMap),"USE_NORMAL_MAP"),
            (nameof(UseSpa),"USE_SPA"),
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


    [UIDragFloat(0.01f, 0, name: "亮度"), Indexable]
    public float Brightness = 1;

    [UIDragFloat(0.01f, 0, name: "天空盒亮度"), Indexable]
    public float SkyLightMultiple = 3;

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
    public int Split;

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

    public void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;

        BoundingFrustum frustum = new BoundingFrustum(ViewProjection);

        pointLights.Clear();

        DirectionalLightData directionalLight = null;
        foreach (var visual in Visuals)
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
            ShadowMapVP = dl.GetLightingMatrix(InvertViewProjection, 0, 0.93f);
            ShadowMapVP1 = dl.GetLightingMatrix(InvertViewProjection, 0.93f, 0.991f);
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

        //renderHelper.PushParameters(this);
        if (directionalLight != null)
        {
            var shadowMap = _ShadowMap;
            drawShadowMap.viewProjection = ShadowMapVP;
            var rect = new Rectangle(0, 0, shadowMap.width / 2, shadowMap.height / 2);
            renderWrap.SetRenderTargetDepth(shadowMap, false);
            renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);

            drawShadowMap.Execute(renderHelper);
            drawShadowMap.viewProjection = ShadowMapVP1;
            rect = new Rectangle(shadowMap.width / 2, 0, shadowMap.width / 2, shadowMap.height / 2);
            renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);

            drawShadowMap.Execute(renderHelper);
        }
        Split = ShadowSize(pointLights.Count * 6);

        if (pointLights.Count > 0)
        {
            var pointLightDatas = CollectionsMarshal.AsSpan(pointLights);
            var rawData = MemoryMarshal.AsBytes(pointLightDatas).ToArray();
            DrawPointShadow(renderHelper, pointLightDatas);

            //drawObject.CBVPerObject[1] = (rawData, pointLights.Count * 32);
            drawObject.additionalSRV[11] = rawData;
            drawObject.keywords.Add(("ENABLE_POINT_LIGHT", "1"));
            drawObject.keywords.Add(("POINT_LIGHT_COUNT", pointLights.Count.ToString()));
        }

        if (debugKeywords.TryGetValue(DebugRenderType, out string debugKeyword))
        {
            drawObject.keywords.Add((debugKeyword, "1"));
        }
        renderWrap.SetRenderTarget(noPostProcess, false);
        drawSkyBox.InvertViewProjection = InvertViewProjection;
        drawSkyBox.CameraPosition = CameraPosition;
        drawSkyBox.SkyLightMultiple = SkyLightMultiple * Brightness;
        drawSkyBox.skybox = _SkyBox;
        drawSkyBox.Execute(renderHelper);

        renderWrap.SetRenderTarget(noPostProcess, depth, false, false);
        drawObject.psoDesc.blendState = BlendState.None;
        drawObject.filter = FilterOpaque;
        drawObject.Execute(renderHelper);

        drawObject.psoDesc.blendState = BlendState.Alpha;
        drawObject.filter = FilterTransparent;
        drawObject.Execute(renderHelper);

        //renderHelper.PopParameters();

        drawObject.keywords.Clear();
    }

    static Matrix4x4 GetShadowMapMatrix(Vector3 pos, Vector3 dir, Vector3 up, float near, float far)
    {
        return Matrix4x4.CreateLookAt(pos, pos + dir, up)
         * Matrix4x4.CreatePerspectiveFieldOfView(1.57079632679f, 1, near, far);
    }

    static void SetViewportScissorRectangle(RenderWrap renderWrap, int index, int split, int width, int height)
    {
        float xOffset = (float)(index % split) / split;
        float yOffset = (float)(index / split) / split;
        float size = 1.0f / split;

        int x = (int)(width * xOffset);
        int y = (int)(height * (yOffset + 0.5f));
        int sizeX1 = (int)(width * size);
        int sizeY1 = (int)(height * size);

        var rect = new Rectangle(x, y, sizeX1, sizeY1);
        renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);
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
        var shadowMap = _ShadowMap;
        renderWrap.SetRenderTargetDepth(shadowMap, false);
        int width = shadowMap.width;
        int height = shadowMap.height;
        foreach (var pl in pointLightDatas)
        {
            var lightRange = pl.Range;
            float near = lightRange * 0.001f;
            float far = lightRange;

            foreach (var val in table)
            {
                drawShadowMap.viewProjection = GetShadowMapMatrix(pl.Position, val.Item1, val.Item2, near, far);
                SetViewportScissorRectangle(renderWrap, index, Split, width, height);
                drawShadowMap.Execute(renderHelper);
                index++;
            }
        }
    }

    static int ShadowSize(int v)
    {
        v *= 2;
        int pointLightSplit = 2;
        for (int i = 4; i * i < v; i += 2)
            pointLightSplit = i;
        pointLightSplit += 2;
        return pointLightSplit;
    }

    static bool FilterOpaque(MeshRenderable renderable)
    {
        if (true.Equals(renderable.material.GetObject("IsTransparent")))
        {
            return false;
        }
        return true;
    }

    static bool FilterTransparent(MeshRenderable renderable)
    {
        if (true.Equals(renderable.material.GetObject("IsTransparent")))
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
