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
                if (!loadTasks.TryGetValue(task, out var loadTask))
                    loadTask = loadTasks[task] = Task.Factory.StartNew(_Task, task);

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

        void _Task(object a)
        {
            var taskParam = (ITextureDecodeTask)a;
            var texturePack = taskParam.TexturePack;
            var datas = taskParam.GetDatas();
            using var stream = new MemoryStream(datas);
            LoadTexture(texturePack, taskParam.GetFileName(), stream, out var uploader);

            taskParam.Uploader = uploader;

            LoadComplete?.Invoke();
        }

        bool LoadTexture(Texture2DPack texturePack, string fileName, Stream stream, out Uploader uploader)
        {
            uploader = null;
            try
            {
                Uploader uploader1 = new Uploader();
                if (texturePack.LoadTexture(fileName, stream, uploader1))
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
