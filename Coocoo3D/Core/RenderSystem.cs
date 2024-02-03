using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace Coocoo3D.Core;

public class RenderSystem
{
    public WindowSystem windowSystem;
    public GraphicsContext graphicsContext;
    public RenderPipelineContext renderPipelineContext;
    public MainCaches mainCaches;

    public List<Type> RenderPipelineTypes = new();

    public void Initialize()
    {
        LoadRenderPipelines(new DirectoryInfo("Effects"));
    }

    List<VisualChannel> channels = new();
    public void Update()
    {
        var context = renderPipelineContext;

        foreach (var channel in windowSystem.visualChannels.Values)
        {
            if (channel.renderPipelineView != null)
                channels.Add(channel);
            else
            {
                channel.DelaySetRenderPipeline(RenderPipelineTypes[0]);
                channels.Add(channel);
            }
        }
        foreach (var visualChannel in channels)
        {
            visualChannel.Onframe((float)context.Time);

            var renderPipelineView = visualChannel.renderPipelineView;
            var renderPipeline = renderPipelineView.renderPipeline;
            renderPipeline.renderWrap.rpc = context;
            foreach (var cap in renderPipelineView.sceneCaptures)
            {
                var member = cap.Value.Item1;
                var captureAttribute = cap.Value.Item2;
                switch (captureAttribute.Capture)
                {
                    case "Camera":
                        member.SetValue(renderPipeline, visualChannel.cameraData);
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
        }

        foreach (var visualChannel in channels)
        {
            var renderPipelineView = visualChannel.renderPipelineView;
            renderPipelineView.renderPipeline.BeforeRender();
        }
        foreach (var visualChannel in channels)
        {
            var renderPipelineView = visualChannel.renderPipelineView;
            renderPipelineView.PrepareRenderResources();
        }
        foreach (var visualChannel in channels)
        {
            var renderPipelineView = visualChannel.renderPipelineView;

            renderPipelineView.renderPipeline.Render();
            renderPipelineView.renderPipeline.AfterRender();
        }
        channels.Clear();
    }


    public void LoadRenderPipelines(DirectoryInfo dir)
    {
        RenderPipelineTypes.Clear();
        foreach (var file in dir.EnumerateFiles("*.dll"))
        {
            LoadRenderPipelineTypes(file.FullName);
        }
    }

    public void LoadRenderPipelineTypes(string path)
    {
        try
        {
            RenderPipelineTypes.AddRange(mainCaches.GetDerivedTypes(Path.GetFullPath(path), typeof(RenderPipeline.RenderPipeline)));
        }
        catch
        {

        }
    }
}