using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using Coocoo3DGraphics;

namespace Coocoo3D.Core
{
    public class RenderSystem
    {
        public WindowSystem windowSystem;
        public GraphicsContext graphicsContext;
        public RenderPipelineContext renderPipelineContext;
        public MainCaches mainCaches;
        public void Update()
        {
            var context = renderPipelineContext;
            while (mainCaches.TextureReadyToUpload.TryDequeue(out var uploadPack))
                graphicsContext.UploadTexture(uploadPack.Item1, uploadPack.Item2);
            while (mainCaches.MeshReadyToUpload.TryDequeue(out var mesh))
                graphicsContext.UploadMesh(mesh);

            var channels = windowSystem.visualChannels.Values;
            foreach (var visualChannel in channels)
            {
                visualChannel.Onframe();
                var renderPipeline = visualChannel.renderPipeline;
                foreach (var cap in visualChannel.renderPipelineView.sceneCaptures)
                {
                    var member = cap.Value.Item1;
                    var captureAttribute = cap.Value.Item2;
                    if (member.GetGetterType() == typeof(CameraData))
                    {
                        member.SetValue(renderPipeline, visualChannel.cameraData);
                    }
                    else if (member.GetGetterType() == typeof(double))
                    {
                        if (member.Name == "Time")
                            member.SetValue(renderPipeline, context.Time);
                        if (member.Name == "DeltaTime")
                            member.SetValue(renderPipeline, context.DeltaTime);
                        if (member.Name == "RealDeltaTime")
                            member.SetValue(renderPipeline, context.RealDeltaTime);
                    }
                    else if (member.GetGetterType() == typeof(bool))
                    {
                        if (member.Name == "Recording")
                            member.SetValue(renderPipeline, context.recording);
                    }
                    if (captureAttribute.Capture == "Visual")
                    {
                        member.SetValue(renderPipeline, renderPipelineContext.visuals);
                    }
                }
            }
            context.gpuWriter.graphicsContext = graphicsContext;
            context.gpuWriter.Clear();

            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                if (renderPipelineView == null) continue;

                renderPipelineView.renderPipeline.BeforeRender();
                renderPipelineView.PrepareRenderResources();
            }
            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                if (renderPipelineView == null) continue;

                renderPipelineView.renderPipeline.Render();
            }

            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                if (renderPipelineView == null) continue;

                renderPipelineView.renderPipeline.AfterRender();
                renderPipelineView.renderWrap.AfterRender();
            }
        }
    }
}