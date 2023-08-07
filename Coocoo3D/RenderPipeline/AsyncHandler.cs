using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline;

public class AsyncHandler<T> : IHandler<T> where T : IAsyncTask
{
    public List<T> Output { get; } = new();
    public Queue<T> inputs = new();

    List<T> Processing = new();
    Dictionary<T, Task> loadTasks = new();

    public object state;

    public int maxProcessingCount = 1;

    public bool Async = true;

    public bool Add(T task)
    {
        inputs.Enqueue(task);
        return true;
    }

    public void Update()
    {
        while (Processing.Count + Output.Count < maxProcessingCount && inputs.TryDequeue(out var input))
        {
            Processing.Add(input);
            try
            {
                input.SyncProcess(state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        if (Async)
        {
            Processing.RemoveAll(task =>
            {
                bool r = false;
                if (!loadTasks.TryGetValue(task, out var loadTask))
                    loadTask = loadTasks[task] = Task.Factory.StartNew(task.Process, state);

                if (loadTask.Status == TaskStatus.RanToCompletion ||
                    loadTask.Status == TaskStatus.Faulted)
                {
                    loadTasks.Remove(task);
                    loadTask.Dispose();
                    Output.Add(task);
                    r = true;
                }
                return r;
            });
        }
        else
        {
            Processing.RemoveAll(task =>
            {
                Output.Add(task);
                return true;
            }
            );
        }
    }
}
