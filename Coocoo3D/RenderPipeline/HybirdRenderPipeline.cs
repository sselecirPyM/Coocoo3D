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

            var passSetting = context.dynamicContextRead.passSetting;

            foreach (var visualChannel in context.visualChannels.Values)
            {
                visualChannel.Onframe(context);
            }
            if (!context.NewRenderPipeline)
            {
                MiscProcess.Process(context, context.gpuWriter);
                context.PrepareRenderTarget(passSetting);
                foreach (var visualChannel in context.visualChannels.Values)
                {
                    visualChannel.PrepareRenderTarget(passSetting, context.outputFormat);
                }

                var dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
                dispatcher?.FrameBegin(context);
            }
            else
            {
                foreach (var visualChannel in context.visualChannels.Values)
                {
                    var renderPipelineView = visualChannel.renderPipelineView;
                    if (renderPipelineView == null) continue;

                    renderPipelineView.renderPipeline.BeforeRender();
                    renderPipelineView.PrepareRenderResources();
                }
            }
            context.gpuWriter.Clear();
        }

        internal static void EndFrame(RenderPipelineContext context)
        {
            var drp = context.dynamicContextRead;
            var passSetting = drp.passSetting;
            var mainCaches = context.mainCaches;
            var dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);

            if (!context.NewRenderPipeline)
            {
                dispatcher?.FrameEnd(context);
            }
            else
            {
                foreach (var visualChannel in context.visualChannels.Values)
                {
                    var renderPipelineView = visualChannel.renderPipelineView;
                    if (renderPipelineView == null) continue;
                    renderPipelineView.renderPipeline.AfterRender();
                    renderPipelineView.renderWrap.AfterRender();
                }
            }
        }

        internal static void RenderCamera(RenderPipelineContext context)
        {
            if (!context.NewRenderPipeline)
            {
                foreach (var visualChannel in context.visualChannels.Values)
                    RenderCamera1(context, visualChannel);
            }
            else
            {
                foreach (var visualChannel in context.visualChannels.Values)
                    RenderCamera2(context, visualChannel);
            }
        }

        internal static void RenderCamera1(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = context.graphicsContext;
            var drp = context.dynamicContextRead;
            var settings = drp.settings;
            var mainCaches = context.mainCaches;
            var passSetting = drp.passSetting;

            context.gpuWriter.Clear();
            UnionShaderParam unionShaderParam = new UnionShaderParam()
            {
                rpc = context,
                passSetting = passSetting,
                graphicsContext = graphicsContext,
                visualChannel = visualChannel,
                GPUWriter = context.gpuWriter,
                settings = settings,
                relativePath = System.IO.Path.GetDirectoryName(passSetting.path),
                mainCaches = mainCaches,
            };
            IPassDispatcher dispatcher = null;
            if (passSetting.Dispatcher != null)
                dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
            dispatcher?.Dispatch(unionShaderParam);
        }

        internal static void RenderCamera2(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var renderPipelineView = visualChannel.renderPipelineView;
            if (renderPipelineView == null) return;
            renderPipelineView.renderPipeline.Render();
        }

        internal static void DispatchPass(UnionShaderParam param)
        {
            var renderSequence = param.renderSequence;
            var pass = param.passSetting.Passes[renderSequence.Name];

            param.pass = pass;
            param.passName = pass.Name;

            var mainCaches = param.mainCaches;

            param.SetRenderTarget();

            param.renderer = null;
            param.material = null;
            bool? executed = mainCaches.GetUnionShader(pass.UnionShader)?.Invoke(param);
        }
    }
}