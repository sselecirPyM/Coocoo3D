using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

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
        "ShadowMapVP",
        "ShadowMapVP1",
        "LightDir",
        0,
        "LightColor",
        0,
        nameof(CameraPosition),
        "SkyLightMultiple",
        "GIVolumePosition",//"GIVolumePosition",
        "RayTracingReflectionQuality",
        "GIVolumeSize",//"GIVolumeSize",
        "RandomI",
        "RayTracingReflectionThreshold"
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
    public int RandomI;

    public PipelineMaterial pipelineMaterial;

    RayTracingShader rayTracingShader;

    public DirectionalLightData directionalLight;

    static readonly string[] missShaders = new[] { "miss" };

    public void SetCamera(CameraData camera)
    {
        this.camera = camera;

        ViewProjection = camera.vpMatrix;
        View = camera.vMatrix;
        Projection = camera.pMatrix;
        InvertViewProjection = camera.pvMatrix;
        CameraPosition = camera.Position;
    }

    public void Execute(RenderHelper context)
    {
        if (rayTracingShader == null)
            Initialize(context);
        RenderWrap renderWrap = context.renderWrap;
        var graphicsContext = renderWrap.graphicsContext;

        context.PushParameters(this);
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
        var rtpso = context.GetRTPSO(keywords1, rayTracingShader,
            Path.GetFullPath(rayTracingShader.hlslFile, renderWrap.BasePath));

        if (!graphicsContext.SetPSO(rtpso))
            return;
        var writer = context.Writer;

        var tpas = new RTTopLevelAcclerationStruct();
        tpas.instances = new();
        Span<byte> bufferData = stackalloc byte[256];
        foreach (var renderable in context.MeshRenderables<ModelMaterial>())
        {
            var material = renderable.material;

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

            inst.SRVs.Add(4, material._Albedo);
            inst.SRVs.Add(5, material._Metallic);
            inst.SRVs.Add(6, material._Roughness);
            inst.SRVs.Add(7, material._Emissive);


            MemoryMarshal.Write(bufferData.Slice(0), Matrix4x4.Transpose(renderable.transform));
            MemoryMarshal.Write(bufferData.Slice(64), material.Metallic);
            MemoryMarshal.Write(bufferData.Slice(64 + 4), material.Roughness);
            MemoryMarshal.Write(bufferData.Slice(64 + 8), material.Emissive);
            MemoryMarshal.Write(bufferData.Slice(64 + 12), material.Specular);
            inst.CBVs.Add(0, bufferData.ToArray());
            tpas.instances.Add(inst);
        }

        int width = renderTarget.width;
        int height = renderTarget.height;

        context.Write(cbv0, writer);
        var cbvData0 = writer.GetData();


        RayTracingCall call = new RayTracingCall();
        call.tpas = tpas;
        call.UAVs = new();
        call.SRVs = new();
        call.CBVs = new();
        call.missShaders = missShaders;

        call.UAVs[0] = renderTarget;
        call.UAVs[1] = pipelineMaterial.GIBufferWrite;

        call.SRVs[1] = pipelineMaterial._Environment;
        call.SRVs[2] = pipelineMaterial._BRDFLUT;
        call.SRVs[3] = pipelineMaterial.depth;
        call.SRVs[4] = pipelineMaterial.gbuffer0;
        call.SRVs[5] = pipelineMaterial.gbuffer1;
        call.SRVs[6] = pipelineMaterial.gbuffer2;
        call.SRVs[7] = pipelineMaterial._ShadowMap;
        call.SRVs[8] = pipelineMaterial.GIBuffer;

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

        context.PopParameters();
    }

    void Initialize(RenderHelper renderHelper)
    {
        var path1 = Path.GetFullPath("RayTracing.json", renderHelper.renderWrap.BasePath);
        using var filestream = File.OpenRead(path1);
        rayTracingShader = RenderHelper.ReadJsonStream<RayTracingShader>(filestream);
    }
}
