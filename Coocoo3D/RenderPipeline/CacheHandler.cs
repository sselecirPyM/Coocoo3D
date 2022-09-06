using Coocoo3D.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class CacheHandler : IHandler<ICacheTask>
    {
        ConcurrentQueue<ICacheTask> cacheTasks = new();
        public MainCaches mainCaches;
        public bool Add(ICacheTask task)
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
                    if (task is INavigableTask navigableTask)
                        navigableTask.OnError(e);
                }

                Output.Add(task);
            }
        }
        public List<ICacheTask> Output { get; } = new();
    }
}
