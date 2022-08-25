using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using glTFLoader.Schema;
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
                    if (modelPack.pmx != null)
                    {
                        GameObject gameObject = new GameObject();
                        gameObject.LoadPmx(modelPack);
                        task.scene.AddGameObject(gameObject);
                    }
                    else
                    {
                        GameObject gameObject = new GameObject();
                        gameObject.Name = Path.GetFileNameWithoutExtension(path);
                        modelPack.LoadMeshComponent(gameObject);
                        task.scene.AddGameObject(gameObject);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                //gameDriver.RequireRender(true);
                Output.Add(task);
            }
        }
    }
}
