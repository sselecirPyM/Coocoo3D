using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System;
using System.Collections.Generic;

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

        public RTPSO RTPSO;

        public static object[] cbv0 =
        {
            "ViewProjection",
            "InvertViewProjection",
            "ShadowMapVP",
            "ShadowMapVP1",
            "LightDir",
            0,
            "LightColor",
            0,
            "CameraPosition",
            "SkyLightMultiple",
            "GIVolumePosition",//"GIVolumePosition",
            "RayTracingReflectionQuality",
            "GIVolumeSize",//"GIVolumeSize",
            "RandomI",
            "RayTracingReflectionThreshold"
        };

        public PipelineMaterial pipelineMaterial;

        public DirectionalLightData directionalLight;

        public Action afterGI;

        public static readonly string[] missShaders = new string[] { "miss" };
    }
}
