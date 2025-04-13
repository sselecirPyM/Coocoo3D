using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.IO;

namespace Coocoo3D.Core;

public class RenderSystem : IDisposable
{
    public MainCaches mainCaches;

    public EngineContext engineContext;

    public List<Type> RenderPipelineTypes = new();

    public Dictionary<string, VisualChannel> visualChannels = new();

    public void Initialize()
    {
        LoadRenderPipelines(new DirectoryInfo("Effects"));
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

    public VisualChannel AddVisualChannel(string name)
    {
        var visualChannel = new VisualChannel();
        visualChannels[name] = visualChannel;
        visualChannel.Name = name;
        engineContext.FillProperties(visualChannel);

        return visualChannel;
    }

    public void RemoveVisualChannel(string name)
    {
        if (visualChannels.Remove(name, out var vc))
        {
            vc.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var vc in visualChannels)
        {
            vc.Value.Dispose();
        }
        visualChannels.Clear();
    }
}