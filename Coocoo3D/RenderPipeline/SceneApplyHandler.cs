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
    public class SceneApplyHandler : IHandler<SceneLoadTask>
    {
        ConcurrentQueue<SceneLoadTask> sceneLoadTasks = new();
        public MainCaches mainCaches;
        public bool Add(SceneLoadTask task)
        {
            sceneLoadTasks.Enqueue(task);
            return true;
        }

        public void Update()
        {
            while (sceneLoadTasks.TryDequeue(out var task))
            {
                try
                {
                    var scene = ReadJsonStream<Coocoo3DScene>(File.OpenRead(task.path));
                    scene.ToScene(task.Scene, mainCaches);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                //gameDriver.RequireRender(true);
                Output.Add(task);
            }
        }
        public List<SceneLoadTask> Output { get; } = new();


        static T ReadJsonStream<T>(Stream stream)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamReader reader1 = new StreamReader(stream);
            return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
        }
    }
}
