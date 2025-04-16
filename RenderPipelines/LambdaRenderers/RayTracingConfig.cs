using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using Coocoo3DGraphics.Commanding;
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

        public PipelineMaterial pipelineMaterial;

        public DirectionalLightData directionalLight;

        public Action afterGI;

        public HitGroupDescription[] hitGroups = new HitGroupDescription[]
        {
            new HitGroupDescription()
            {
                name = "rayHit",
                closestHit = "closestHit"
            }
        };

        public Action<ComputeCommandProxy> Binding;

        public static readonly string[] missShaders = new string[] { "miss" };
    }
}
