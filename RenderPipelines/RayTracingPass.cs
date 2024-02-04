using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace RenderPipelines;

public class RayTracingPass
{
    public CameraData camera;

    public bool RayTracing;

    public bool RayTracingGI;

    public bool UseGI;

    public Texture2D renderTarget;

    Random random = new Random(0);

    List<(string, string)> keywords1 = new();

    object[] cbv0 =
    {
        nameof(ViewProjection),
        nameof(InvertViewProjection),
        nameof(CameraPosition),
        "SkyLightMultiple",
        "GIVolumePosition",//"GIVolumePosition",
        "RayTracingReflectionQuality",
        "GIVolumeSize",//"GIVolumeSize",
        "RandomI",
        "RayTracingReflectionThreshold"
    };

    object[] cbv1 =
    {
        null,//transform
        "ShadowMapVP",
        "ShadowMapVP1",
        "LightDir",
        0,
        "LightColor",
        0,
        "Metallic",
        "Roughness",
        "Emissive",
        "Specular"
    };

    public string[] srvs;

    object[] uavs = { null, null };

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
    public int RandomI;

    RayTracingShader rayTracingShader;

    public DirectionalLightData directionalLight;

    static readonly string[] missShaders = new[] { "miss" };

    string[] localSrvs = new string[]
    {
        "_Albedo",
        "_Emissive",
        "_Metallic",
        "_Roughness",
        "_Roughness"
    };

    public void SetCamera(CameraData camera)
    {
        this.camera = camera;

        ViewProjection = camera.vpMatrix;
        View = camera.vMatrix;
        Projection = camera.pMatrix;
        InvertViewProjection = camera.pvMatrix;
        CameraPosition = camera.Position;
    }

    public void Execute(RenderHelper renderHelper)
    {
        if (rayTracingShader == null)
            Initialize(renderHelper);
        RenderWrap renderWrap = renderHelper.renderWrap;
        var graphicsContext = renderWrap.graphicsContext;

        renderHelper.PushParameters(this);
        RandomI = random.Next();

        keywords1.Clear();
        if (directionalLight != null)
        {
            keywords1.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
        }
        if (UseGI)
        {
            keywords1.Add(new("ENABLE_GI", "1"));
        }
        var rtpso = renderHelper.GetRTPSO(keywords1, rayTracingShader,
            Path.GetFullPath(rayTracingShader.hlslFile, renderWrap.BasePath));

        if (!graphicsContext.SetPSO(rtpso))
            return;
        var writer = renderHelper.Writer;

        var tpas = new RTTopLevelAcclerationStruct();
        tpas.instances = new();
        foreach (var renderable in renderHelper.MeshRenderables(false))
        {
            var material = renderable.material;
            cbv1[0] = renderable.transform;
            renderHelper.Write(cbv1, writer, material);
            var cbvData1 = writer.GetData();

            var btas = new RTBottomLevelAccelerationStruct();

            btas.mesh = renderable.mesh;

            btas.indexStart = renderable.indexStart;
            btas.indexCount = renderable.indexCount;
            btas.vertexStart = renderable.vertexStart;
            btas.vertexCount = renderable.vertexCount;
            var inst = new RTInstance() { accelerationStruct = btas };
            inst.transform = renderable.transform;
            inst.hitGroupName = "rayHit";
            inst.SRVs = new();
            inst.CBVs = new();
            for (int i = 0; i < localSrvs.Length; i++)
            {
                inst.SRVs.Add(i + 4, renderWrap.GetTex2DFallBack(localSrvs[i], material));
            }

            inst.CBVs.Add(0, cbvData1);
            tpas.instances.Add(inst);
        }

        int width = renderTarget.width;
        int height = renderTarget.height;
        uavs[0] = renderTarget;
        uavs[1] = renderWrap.GetResourceFallBack("GIBufferWrite");

        renderHelper.Write(cbv0, writer);
        var cbvData0 = writer.GetData();


        RayTracingCall call = new RayTracingCall();
        call.tpas = tpas;
        call.UAVs = new();
        call.SRVs = new();
        call.CBVs = new();
        call.missShaders = missShaders;
        for (int i = 0; i < uavs.Length; i++)
        {
            call.UAVs[i] = uavs[i];
        }

        for (int i = 0; i < srvs.Length; i++)
        {
            string srv = srvs[i];
            if (srv == null)
                continue;
            call.SRVs[i] = renderWrap.GetResourceFallBack(srv);
        }

        call.CBVs.Add(0, cbvData0);

        graphicsContext.BuildAccelerationStruct(tpas);
        if (RayTracingGI)
        {
            call.rayGenShader = "rayGenGI";
            graphicsContext.DispatchRays(16, 16, 16, call);
            renderWrap.Swap("GIBuffer", "GIBufferWrite");
        }
        if (RayTracing)
        {
            call.rayGenShader = "rayGen";
            graphicsContext.DispatchRays(width, height, 1, call);
        }

        foreach (var inst in tpas.instances)
            inst.accelerationStruct.Dispose();
        tpas.Dispose();
        renderHelper.PopParameters();
    }

    void Initialize(RenderHelper renderHelper)
    {
        var path1 = Path.GetFullPath("RayTracing.json", renderHelper.renderWrap.BasePath);
        using var filestream = File.OpenRead(path1);
        rayTracingShader = RenderHelper.ReadJsonStream<RayTracingShader>(filestream);
    }
}
