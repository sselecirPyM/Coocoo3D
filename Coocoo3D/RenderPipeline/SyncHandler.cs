using System;
using System.Collections.Generic;

namespace Coocoo3D.RenderPipeline;

public class SyncHandler<T> : IHandler<T> where T : ISyncTask
{
    public List<T> Output { get; } = new();
    public Queue<T> inputs = new();

    List<T> Processing = new();

    public object state;

    public int maxProcessingCount = 1;

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
                input.Process(state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        Processing.RemoveAll(task =>
        {
            Output.Add(task);
            return true;
        });
    }
}
