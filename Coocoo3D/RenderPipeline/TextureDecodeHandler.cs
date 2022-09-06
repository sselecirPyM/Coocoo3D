using Coocoo3D.Core;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class TextureDecodeHandler : IHandler<ITextureDecodeTask>
    {
        public List<ITextureDecodeTask> Output { get; } = new();

        public Queue<ITextureDecodeTask> inputs = new();
        List<ITextureDecodeTask> Processing = new();
        Dictionary<ITextureDecodeTask, Task> loadTasks = new();

        public Action LoadComplete;
        public bool Add(ITextureDecodeTask task)
        {
            inputs.Enqueue(task);
            var task1 = (TextureLoadTask)task;
            if (task1.texture != null)
                task1.texture.Status = GraphicsObjectStatus.loading;
            return true;
        }

        public void Update()
        {
            while (Processing.Count + Output.Count < 9 && inputs.TryDequeue(out var input))
            {
                Processing.Add(input);
            }

            Processing.RemoveAll(task =>
            {
                bool r = false;
                if (!loadTasks.TryGetValue(task, out var loadTask))
                    loadTask = loadTasks[task] = Task.Factory.StartNew((object a) =>
                    {
                        var taskParam = (TextureLoadTask)a;
                        var texturePack = taskParam.pack;
                        var knownFile = taskParam.knownFile;

                        LoadTexture(texturePack, knownFile, out var uploader);

                        taskParam.uploader = uploader;

                        LoadComplete?.Invoke();
                    }, task);

                if (loadTask.Status == TaskStatus.RanToCompletion ||
                    loadTask.Status == TaskStatus.Faulted)
                {
                    loadTasks.Remove(task);
                    Output.Add(task);
                    r = true;
                }
                return r;
            });
        }


        bool LoadTexture(Texture2DPack texturePack, KnownFile knownFile, out Uploader uploader)
        {
            uploader = null;
            try
            {
                Uploader uploader1 = new Uploader();
                if (texturePack.ReloadTexture(knownFile.file, uploader1))
                {
                    uploader = uploader1;
                    texturePack.Status = GraphicsObjectStatus.loaded;
                    return true;
                }
                texturePack.Status = GraphicsObjectStatus.error;
                return false;
            }
            catch
            {
                texturePack.Status = GraphicsObjectStatus.error;
                return false;
            }
        }
    }
}
