using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.LambdaRenderers
{
    public class RayTracingConfig
    {
        public CameraData camera;

        public bool RayTracing;

        public bool RayTracingGI;

        public bool UseGI;

        public Texture2D renderTarget;

        public List<(string, string)> keywords1 = new();

        public static object[] cbv0 =
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

        public PipelineMaterial pipelineMaterial;

        public DirectionalLightData directionalLight;

        public static readonly string[] missShaders = new string[] { "miss" };
    }
}
