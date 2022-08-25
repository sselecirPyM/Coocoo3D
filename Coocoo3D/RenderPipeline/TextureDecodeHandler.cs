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
    public class TextureDecodeHandler : IHandler<TextureLoadTask>
    {
        public List<TextureLoadTask> Output { get; } = new();

        public MainCaches mainCaches;

        public Queue<TextureLoadTask> inputs = new();
        List<TextureLoadTask> Processing = new();

        public Action LoadComplete;

        public bool Add(TextureLoadTask task)
        {
            inputs.Enqueue(task);
            task.pack.Status = GraphicsObjectStatus.loading;
            if (task.texture != null)
                task.texture.Status = GraphicsObjectStatus.loading;
            return true;
        }

        public void Update()
        {
            while (Processing.Count + Output.Count < 6 && inputs.TryDequeue(out var input))
            {
                Processing.Add(input);
            }

            Processing.RemoveAll(task =>
            {
                bool r = false;
                if (task.loadTask == null)
                    task.loadTask = Task.Factory.StartNew((object a) =>
                    {
                        var taskParam = (TextureLoadTask)a;
                        var texturePack = taskParam.pack;
                        var knownFile = taskParam.knownFile;
                        var folderPath = Path.GetDirectoryName(knownFile.fullPath);
                        var folder = new DirectoryInfo(folderPath);

                        if (LoadTexture(folder, texturePack, knownFile, out var uploader))
                            texturePack.Status = GraphicsObjectStatus.loaded;
                        else
                            texturePack.Status = GraphicsObjectStatus.error;
                        taskParam.uploader = uploader;

                        LoadComplete?.Invoke();
                    }, task);
                var loadTask = task.loadTask;
                if (loadTask.Status == TaskStatus.RanToCompletion ||
                    loadTask.Status == TaskStatus.Faulted)
                {
                    Output.Add(task);
                    r = true;
                }
                return r;
            });
        }


        bool LoadTexture(DirectoryInfo folder, Texture2DPack texturePack, KnownFile knownFile, out Uploader uploader)
        {
            uploader = null;
            try
            {
                if (!knownFile.IsModified(folder.GetFiles()) && texturePack.initialized)
                    return true;
                Uploader uploader1 = new Uploader();
                if (texturePack.ReloadTexture(knownFile.file, uploader1))
                {
                    uploader = uploader1;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
