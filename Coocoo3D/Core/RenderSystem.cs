using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        public void Update()
        {
            var context = windowSystem.RenderPipelineContext;
            var mainCaches = context.mainCaches;
            while (mainCaches.TextureReadyToUpload.TryDequeue(out var uploadPack))
                graphicsContext.UploadTexture(uploadPack.Item1, uploadPack.Item2);
            while (mainCaches.MeshReadyToUpload.TryDequeue(out var mesh))
                graphicsContext.UploadMesh(mesh);
            var drp = context.dynamicContextRead;
            var channels = windowSystem.visualChannels.Values;
            foreach (var visualChannel in channels)
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