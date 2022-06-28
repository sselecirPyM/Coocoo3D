using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Caprice.Attributes;
using System.IO;

namespace Coocoo3D.RenderPipeline
{
    public class VisualChannel : IDisposable
    {
        public string Name;
        public Camera camera = new Camera();
        public CameraData cameraData;
        public (int, int) outputSize = (100, 100);
        public (int, int) sceneViewSize = (100, 100);

        public RenderPipeline renderPipeline;
        public RenderPipelineView renderPipelineView;

        public Type newRenderPipelineType;
        public string newRenderPipelinePath;
        public RenderPipelineContext rpc;

        public Dictionary<string, object> pipelineSettings = new();

        public VisualChannel()
        {
        }

        public void Onframe()
        {
            if (newRenderPipelineType != null)
            {
                if (renderPipelineView != null)
                {
                    renderPipelineView.Export(pipelineSettings);
                }
                if (renderPipeline is IDisposable disposable0)
                    disposable0.Dispose();
                renderPipelineView?.Dispose();

                SetRenderPipeline((RenderPipeline)Activator.CreateInstance(newRenderPipelineType),
                    newRenderPipelinePath);
                newRenderPipelineType = null;
            }

            if (camera.CameraMotionOn) camera.SetCameraMotion((float)rpc.dynamicContext.Time);
            cameraData = camera.GetCameraData();
        }

        public void DelaySetRenderPipeline(Type type)
        {
            newRenderPipelinePath = Path.GetDirectoryName(type.Assembly.Location);
            this.newRenderPipelineType = type;
        }

        void SetRenderPipeline(RenderPipeline renderPipeline, string basePath)
        {
            this.renderPipeline = renderPipeline;
            var renderPipelineView = new RenderPipelineView(renderPipeline, basePath);
            this.renderPipelineView = renderPipelineView;
            var renderWrap = new RenderWrap()
            {
                RenderPipelineView = renderPipelineView,
                visualChannel = this,
                rpc = rpc,
            };
            renderPipeline.renderWrap = renderWrap;
            renderPipelineView.renderWrap = renderWrap;
            renderPipelineView.Import(pipelineSettings);
        }

        public Texture2D GetAOV(AOVType type)
        {
            var aov = renderPipelineView?.GetAOV(type);
            if (aov != null)
                return aov;
            else
                return null;
        }

        public void Dispose()
        {
            renderPipelineView?.Dispose();
        }
    }
}
