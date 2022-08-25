using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class SceneSaveHandler : IHandler<SceneSaveTask>
    {
        ConcurrentQueue<SceneSaveTask> sceneSaveTasks = new();
        public List<SceneSaveTask> Output { get; } = new();

        public bool Add(SceneSaveTask task)
        {
            sceneSaveTasks.Enqueue(task);
            return true;
        }

        public void Update()
        {
            while (sceneSaveTasks.TryDequeue(out var task))
            {
                try
                {
                    var scene = Coocoo3DScene.FromScene(task.Scene);

                    SaveJsonStream(new FileInfo(task.path).Create(), scene);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                //gameDriver.RequireRender(true);
                Output.Add(task);
            }
        }

        static void SaveJsonStream<T>(Stream stream, T val)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamWriter writer = new StreamWriter(stream);
            jsonSerializer.Serialize(writer, val);
        }
    }
}
