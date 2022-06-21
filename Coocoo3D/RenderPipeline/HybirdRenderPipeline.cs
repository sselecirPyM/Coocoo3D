using Coocoo3D.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public static class HybirdRenderPipeline
    {
        internal static void BeginFrame(RenderPipelineContext context)
        {
            var mainCaches = context.mainCaches;
            var graphicsContext = context.graphicsContext;
            context.gpuWriter.graphicsContext = graphicsContext;
            while (mainCaches.TextureReadyToUpload.TryDequeue(out var uploadPack))
                graphicsContext.UploadTexture(uploadPack.Item1, uploadPack.Item2);
            while (mainCaches.MeshReadyToUpload.TryDequeue(out var mesh))
                graphicsContext.UploadMesh(mesh);
            var drp = context.dynamicContextRead;
            foreach (var visualChannel in context.visualChannels.Values)
            {
                visualChannel.Onframe(context);

                foreach (var cap in visualChannel.renderPipelineView.sceneCaptures)
                {
                    var member = cap.Value;
                    if (member.GetGetterType() == typeof(CameraData))
                    {
                        member.SetValue(visualChannel.renderPipeline, visualChannel.cameraData);
                    }
                    else if (member.GetGetterType() == typeof(double))
                    {
                        if (member.Name == "Time")
                            member.SetValue(visualChannel.renderPipeline, drp.Time);
                        if (member.Name == "DeltaTime")
                            member.SetValue(visualChannel.renderPipeline, drp.DeltaTime);
                        if (member.Name == "RealDeltaTime")
                            member.SetValue(visualChannel.renderPipeline, drp.RealDeltaTime);
                    }
                }
            }
            context.gpuWriter.Clear();
        }

        internal static void EndFrame(RenderPipelineContext context)
        {
            foreach (var visualChannel in context.visualChannels.Values)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                if (renderPipelineView == null) continue;
                renderPipelineView.renderPipeline.AfterRender();
                renderPipelineView.renderWrap.AfterRender();
            }
        }

        internal static void RenderCamera(RenderPipelineContext context)
        {
            foreach (var visualChannel in context.visualChannels.Values)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                if (renderPipelineView == null) continue;

                renderPipelineView.renderPipeline.BeforeRender();
                renderPipelineView.PrepareRenderResources();
            }
            foreach (var visualChannel in context.visualChannels.Values)
                RenderCamera2(context, visualChannel);
        }

        internal static void RenderCamera2(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var renderPipelineView = visualChannel.renderPipelineView;
            if (renderPipelineView == null) return;
            renderPipelineView.renderPipeline.Render();
        }
    }
}