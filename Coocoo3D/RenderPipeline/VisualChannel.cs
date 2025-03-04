using Caprice.Attributes;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.Utility;
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

public interface IVisualChannelAttach
{
    public void OnRender(VisualChannel visualChannel);
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
    public RenderPipelineContext context;
    public EngineContext engineContext;

    public bool disposed;

    public HashSet<IVisualChannelAttach> attaches = new HashSet<IVisualChannelAttach>();

    public void Attach(IVisualChannelAttach attach)
    {
        attaches.Remove(attach);
        attaches.Add(attach);
    }
    public void Detach(IVisualChannelAttach attach)
    {
        attaches.Remove(attach);
    }

    public void SetRenderPipeline(Type type)
    {
        this.renderPipelineView?.Dispose();

        RenderPipeline renderPipeline = (RenderPipeline)Activator.CreateInstance(type);
        string basePath = Path.GetDirectoryName(type.Assembly.Location);

        this.renderPipelineView = new RenderPipelineView(renderPipeline, MainCaches, basePath);
        engineContext.FillProperties(renderPipelineView);
        renderPipeline.renderPipelineView = renderPipelineView;
    }

    public void UpdateSize()
    {
        if (resolusionSizeSource == ResolusionSizeSource.Custom)
            return;
        outputSize = sceneViewSize;
        (float x, float y) = outputSize;
        camera.AspectRatio = x / y;
    }

    public void Render()
    {
        if (camera.CameraMotionOn)
            camera.SetCameraMotion((float)context.Time);
        cameraData = camera.GetCameraData();

        renderPipelineView.outputSize = outputSize;
        var renderPipeline = renderPipelineView.renderPipeline;
        renderPipelineView.rpc = context;
        foreach (var cap in renderPipelineView.sceneCaptures)
        {
            var member = cap.Value.Item1;
            var captureAttribute = cap.Value.Item2;
            switch (captureAttribute.Capture)
            {
                case "Camera":
                    member.SetValue(renderPipeline, cameraData);
                    break;
                case "Time":
                    member.SetValue(renderPipeline, context.Time);
                    break;
                case "DeltaTime":
                    member.SetValue(renderPipeline, context.DeltaTime);
                    break;
                case "RealDeltaTime":
                    member.SetValue(renderPipeline, context.RealDeltaTime);
                    break;
                case "Recording":
                    member.SetValue(renderPipeline, context.recording);
                    break;
                case "Visual":
                    member.SetValue(renderPipeline, context.visuals);
                    break;
            }
        }

        renderPipelineView.renderPipeline.BeforeRender();
        renderPipelineView.PrepareRenderResources();
        renderPipelineView.renderPipeline.Render();
        renderPipelineView.renderPipeline.AfterRender();

        foreach(var attach in attaches)
        {
            attach.OnRender(this);
        }
    }

    public Texture2D GetAOV(AOVType type)
    {
        return renderPipelineView?.GetAOV(type);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        foreach (var attach in attaches)
        {
            if (attach is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        attaches.Clear();

        disposed = true;
        renderPipelineView?.Dispose();
    }
}
