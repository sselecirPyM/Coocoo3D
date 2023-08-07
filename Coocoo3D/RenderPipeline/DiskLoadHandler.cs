using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline;

public class DiskLoadHandler : IHandler<TextureLoadTask>
{
    ConcurrentQueue<TextureLoadTask> diskLoadTasks = new();

    public List<TextureLoadTask> Output { get; } = new();
    List<TextureLoadTask> Processing = new();
    Dictionary<TextureLoadTask, Task<byte[]>> loadTasks = new();

    public Action LoadComplete;
    public bool Add(TextureLoadTask task)
    {
        diskLoadTasks.Enqueue(task);
        task.OnEnterPipeline();
        return true;
    }

    public void Update()
    {
        while (Processing.Count + Output.Count < 16 && Processing.Count < 8 && diskLoadTasks.TryDequeue(out var task))
        {
            Processing.Add(task);
        }

        Processing.RemoveAll(task =>
        {
            bool r = false;

            if (!loadTasks.TryGetValue(task, out var loadTask))
                loadTask = loadTasks[task] = File.ReadAllBytesAsync(task.KnownFile.fullPath);

            if (loadTask.Status == TaskStatus.RanToCompletion ||
                loadTask.Status == TaskStatus.Faulted)
            {
                if (loadTask.Status == TaskStatus.RanToCompletion)
                    task.Datas = loadTask.Result;
                else
                    task.OnError(loadTask.Exception);
                loadTasks.Remove(task);
                loadTask.Dispose();
                Output.Add(task);
                r = true;
            }
            return r;
        });
    }
}
