using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWrap;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class ModelLoadHandler : IHandler<ModelLoadTask>
    {
        ConcurrentQueue<ModelLoadTask> modelLoadTasks = new();
        public List<ModelLoadTask> Output { get; } = new();

        public MainCaches mainCaches;

        public bool Add(ModelLoadTask task)
        {
            modelLoadTasks.Enqueue(task);
            return true;
        }

        public void Update()
        {
            while (modelLoadTasks.TryDequeue(out var task))
            {
                try
                {
                    string path = task.path;
                    ModelPack modelPack = mainCaches.GetModel(path);
                    foreach (var tex in modelPack.textures)
                        mainCaches.PreloadTexture(tex);

                    var world = task.scene.recorder.Record(task.scene.world);

                    if (modelPack.pmx != null)
                    {
                        var gameObject = world.CreateEntity();
                        gameObject.LoadPmx(modelPack);
                    }
                    else
                    {
                        var entity = world.CreateEntity();
                        modelPack.LoadMeshComponent(entity);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                Output.Add(task);
            }
        }
    }
}
