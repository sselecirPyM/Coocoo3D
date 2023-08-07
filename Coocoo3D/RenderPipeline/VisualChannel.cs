using Caprice.Attributes;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace Coocoo3D.RenderPipeline;

public enum ResolusionSizeSource
{
    Default = 0,
    Custom = 1,
}
public class VisualChannel : IDisposable
{
    public string Name;
    public MainCaches MainCaches;
    public Camera camera = new Camera();
    public CameraData cameraData;
    public ResolusionSizeSource resolusionSizeSource;
    public (int, int) outputSize = (100, 100);
    public (int, int) sceneViewSize = (100, 100);

    public RenderPipelineView renderPipelineView;

    public Type newRenderPipelineType;
    public string newRenderPipelinePath;

    public bool disposed;

    Dictionary<string, object> pipelineSettings = new();

    public void Onframe(float time)
    {
        if (newRenderPipelineType != null)
        {
            if (renderPipelineView != null)
            {
                renderPipelineView.Export(pipelineSettings);
            }
            renderPipelineView?.Dispose();

            SetRenderPipeline((RenderPipeline)Activator.CreateInstance(newRenderPipelineType), newRenderPipelinePath);
            newRenderPipelineType = null;
        }

        if (camera.CameraMotionOn)
            camera.SetCameraMotion(time);
        cameraData = camera.GetCameraData();

        if (renderPipelineView != null)
            renderPipelineView.renderPipeline.renderWrap.outputSize = outputSize;
    }

    public void DelaySetRenderPipeline(Type type)
    {
        newRenderPipelinePath = Path.GetDirectoryName(type.Assembly.Location);
        this.newRenderPipelineType = type;
    }

    void SetRenderPipeline(RenderPipeline renderPipeline, string basePath)
    {
        var renderPipelineView = new RenderPipelineView(renderPipeline, MainCaches, basePath);
        this.renderPipelineView = renderPipelineView;
        var renderWrap = new RenderWrap()
        {
            RenderPipelineView = renderPipelineView,
        };
        renderPipeline.renderWrap = renderWrap;
        renderPipelineView.renderWrap = renderWrap;
        renderPipelineView.Import(pipelineSettings);
    }

    public void UpdateSize()
    {
        if (resolusionSizeSource == ResolusionSizeSource.Custom)
            return;
        outputSize = sceneViewSize;
        (float x, float y) = outputSize;
        camera.AspectRatio = x / y;
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
        disposed = true;
    }
}
