using Caprice.Attributes;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class RayTracingPass
    {
        public string RayTracingShader;

        public CameraData camera;

        public bool RayTracing;

        public bool RayTracingGI;

        public bool UseGI;

        //public bool volumeLighting;

        public string RenderTarget;

        Random random = new Random(0);

        public List<(string, string)> keywords = new();

        object[] cbv0 =
        {
            nameof(ViewProjection),
            nameof(InvertViewProjection),
            nameof(CameraPosition),
            "SkyLightMultiple",
            Vector3.Zero,//"GIVolumePosition",
            "RayTracingReflectionQuality",
            Vector3.Zero,//"GIVolumeSize",
            "RandomI",
            "RayTracingReflectionThreshold"
        };

        object[] cbv1 =
        {
            null,//trnsform
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

        private string[] uavs = { null };

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

        public void SetCamera(CameraData camera)
        {
            this.camera = camera;


            ViewProjection = camera.vpMatrix;
            View = camera.vMatrix;
            Projection = camera.pMatrix;
            InvertViewProjection = camera.pvMatrix;
            CameraPosition = camera.Position;
        }

        public void Execute(RenderWrap renderWrap)
        {
            var graphicsContext = renderWrap.graphicsContext;
            var mainCaches = renderWrap.rpc.mainCaches;


            var path1 = Path.GetFullPath(RayTracingShader, renderWrap.BasePath);
            var rayTracingShader = mainCaches.GetRayTracingShader(path1);

            var directionalLights = renderWrap.directionalLights;
            var pointLights = renderWrap.pointLights;
            renderWrap.PushParameters(this);
            RandomI = random.Next();

            List<ValueTuple<string, string>> keywords = new(this.keywords);
            if (directionalLights.Count != 0)
            {
                keywords.Add(new("ENABLE_DIRECTIONAL_LIGHT", "1"));
                //if (volumeLighting)
                //    keywords.Add(new("ENABLE_VOLUME_LIGHTING", "1"));
            }
            if (UseGI)
                keywords.Add(new("ENABLE_GI", "1"));
            var rtpso = mainCaches.GetRTPSO(keywords,
            rayTracingShader,
            Path.GetFullPath(rayTracingShader.hlslFile, Path.GetDirectoryName(path1)));

            if (!graphicsContext.SetPSO(rtpso)) return;
            var writer = renderWrap.Writer;

            var tpas = new RTTopLevelAcclerationStruct();
            tpas.instances = new();
            foreach (var renderable in renderWrap.MeshRenderables())
            {
                cbv1[0] = renderable.transform;
                renderWrap.Write(cbv1, writer);
                var cbvData1 = writer.GetData();

                var material = renderable.material;
                var btas = new RTBottomLevelAccelerationStruct();

                btas.mesh = renderable.mesh;
                btas.meshOverride = renderable.meshOverride;
                btas.indexStart = renderable.indexStart;
                btas.indexCount = renderable.indexCount;
                btas.vertexStart = renderable.vertexStart;
                btas.vertexCount = renderable.vertexCount;
                var inst = new RTInstance() { accelerationStruct = btas };
                inst.transform = renderable.transform;
                inst.hitGroupName = "rayHit";
                inst.SRVs = new();
                inst.SRVs.Add(4, renderWrap.GetTex2DFallBack("_Albedo", material));
                inst.SRVs.Add(5, renderWrap.GetTex2DFallBack("_Emissive", material));
                inst.SRVs.Add(6, renderWrap.GetTex2DFallBack("_Metallic", material));
                inst.SRVs.Add(7, renderWrap.GetTex2DFallBack("_Roughness", material));
                inst.CBVs = new();
                inst.CBVs.Add(0, cbvData1);
                tpas.instances.Add(inst);
            }
            uavs[0] = RenderTarget;
            Texture2D renderTarget = renderWrap.GetRenderTexture2D(RenderTarget);
            int width = renderTarget.width;
            int height = renderTarget.height;

            renderWrap.Write(cbv0, writer);
            var cbvData0 = writer.GetData();


            RayTracingCall call = new RayTracingCall();
            call.tpas = tpas;
            call.UAVs = new();
            for (int i = 0; i < uavs.Length; i++)
            {
                string uav = uavs[i];
                call.UAVs[i] = renderWrap.GetRenderTexture2D(uav);
            }

            call.SRVs = new();
            for (int i = 0; i < srvs.Length; i++)
            {
                string srv = srvs[i];
                if (srv == null) continue;
                if (i == 1)
                    call.SRVs[i] = renderWrap.GetTexCube(srv);
                else
                    call.SRVs[i] = renderWrap.GetTex2DFallBack(srv);
                if (renderWrap.SlotIsLinear(srv))
                    call.srvFlags[i] = 1;
            }

            call.CBVs = new();
            call.CBVs.Add(0, cbvData0);
            call.missShaders = new[] { "miss" };

            //if (RayTracingGI)
            //{
            //    call.rayGenShader = "rayGenGI";
            //    graphicsContext.DispatchRays(16, 16, 16, call);
            //    //param.SwapBuffer("GIBuffer", "GIBufferWrite");
            //}
            if (RayTracing)
            {
                call.rayGenShader = "rayGen";
                graphicsContext.DispatchRays(width, height, 1, call);
            }

            foreach (var inst in tpas.instances)
                inst.accelerationStruct.Dispose();
            tpas.Dispose();
            renderWrap.PopParameters();
        }
    }
}
