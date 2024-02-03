using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Coocoo3D.RenderPipeline;

public class CacheHandler : IHandler<TextureLoadTask>
{
    ConcurrentQueue<TextureLoadTask> cacheTasks = new();
    public MainCaches mainCaches;
    public bool Add(TextureLoadTask task)
    {
        cacheTasks.Enqueue(task);
        return true;
    }

    public void Update()
    {
        while (cacheTasks.TryDequeue(out var task))
        {
            try
            {
                var knownFile = mainCaches.GetFileInfo(task.CachePath);
                var folderPath = Path.GetDirectoryName(knownFile.fullPath);
                var folder = new DirectoryInfo(folderPath);
                if (knownFile.IsModified(folder.GetFiles()))
                {
                    task.CacheInvalid();
                }
            }
            catch (Exception e)
            {
                if (task is TextureLoadTask navigableTask)
                    navigableTask.OnError(e);
            }

            Output.Add(task);
        }
    }
    public List<TextureLoadTask> Output { get; } = new();
}
