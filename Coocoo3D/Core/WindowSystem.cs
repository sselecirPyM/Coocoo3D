using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;

namespace Coocoo3D.Core;

public class WindowSystem : IDisposable
{
    public Dictionary<string, VisualChannel> visualChannels = new();

    public MainCaches MainCaches;

    Queue<string> delayAddVisualChannel = new();
    Queue<string> delayRemoveVisualChannel = new();

    public void UpdateChannels()
    {
        foreach (var visualChannel1 in visualChannels)
        {
            var visualChannel = visualChannel1.Value;
            visualChannel.UpdateSize();
        }
    }

    public void DelayAddVisualChannel(string name)
    {
        delayAddVisualChannel.Enqueue(name);
    }
    public void DelayRemoveVisualChannel(string name)
    {
        delayRemoveVisualChannel.Enqueue(name);
    }

    public void Update()
    {
        while (delayAddVisualChannel.TryDequeue(out var vcName))
            AddVisualChannel(vcName);
        while (delayRemoveVisualChannel.TryDequeue(out var vcName))
            RemoveVisualChannel(vcName);
    }

    public void Dispose()
    {
        foreach (var vc in visualChannels)
        {
            vc.Value.Dispose();
        }
        visualChannels.Clear();
    }

    public VisualChannel AddVisualChannel(string name)
    {
        var visualChannel = new VisualChannel();
        visualChannels[name] = visualChannel;
        visualChannel.Name = name;
        visualChannel.MainCaches = MainCaches;

        return visualChannel;
    }

    void RemoveVisualChannel(string name)
    {
        if (visualChannels.Remove(name, out var vc))
        {
            vc.Dispose();
        }
    }
}
