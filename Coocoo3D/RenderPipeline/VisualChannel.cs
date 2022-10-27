using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caprice.Attributes;
using System.IO;

namespace Coocoo3D.RenderPipeline
{
    public enum ResolusionSizeSource
    {
        Default = 0,
        Custom = 1,
    }
    public class VisualChannel : IDisposable
    {
        public string Name;
        public Camera camera = new Camera();
        public CameraData cameraData;
        public ResolusionSizeSource resolusionSizeSource;
        public (int, int) outputSize = (100, 100);
        public (int, int) sceneViewSize = (100, 100);

        public RenderPipeline renderPipeline;
        public RenderPipelineView renderPipelineView;

        public Type newRenderPipelineType;
        public string newRenderPipelinePath;

        Dictionary<string, object> pipelineSettings = new();

        public void Onframe(float time, MainCaches caches)
        {
            if (newRenderPipelineType != null)
            {
                if (renderPipelineView != null)
                {
                    renderPipelineView.Export(pipelineSettings, caches.GetUIUsage(renderPipelineView.renderPipeline.GetType()));
                }
                if (renderPipeline is IDisposable disposable1)
                    disposable1.Dispose();
                renderPipelineView?.Dispose();

                SetRenderPipeline((RenderPipeline)Activator.CreateInstance(newRenderPipelineType),
                    newRenderPipelinePath, caches);
                newRenderPipelineType = null;
            }

            if (camera.CameraMotionOn)
                camera.SetCameraMotion(time);
            cameraData = camera.GetCameraData();

            if (this.renderPipeline != null)
                this.renderPipeline.renderWrap.outputSize = outputSize;
        }

        public void DelaySetRenderPipeline(Type type)
        {
            newRenderPipelinePath = Path.GetDirectoryName(type.Assembly.Location);
            this.newRenderPipelineType = type;
        }

        void SetRenderPipeline(RenderPipeline renderPipeline, string basePath, MainCaches caches)
        {
            this.renderPipeline = renderPipeline;
            var renderPipelineView = new RenderPipelineView(renderPipeline, basePath);
            this.renderPipelineView = renderPipelineView;
            var renderWrap = new RenderWrap()
            {
                RenderPipelineView = renderPipelineView,
            };
            renderPipeline.renderWrap = renderWrap;
            renderPipelineView.renderWrap = renderWrap;
            renderPipelineView.Import(pipelineSettings, caches.GetUIUsage(renderPipelineView.renderPipeline.GetType()));
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
            if (renderPipeline is IDisposable disposable1)
                disposable1.Dispose();
        }
    }
}
