using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public class GPUUploadHandler : IHandler<TextureLoadTask>
    {
        public GraphicsContext graphicsContext;

        public bool Add(TextureLoadTask task)
        {
            inputs.Enqueue(task);
            return true;
        }

        public void Update()
        {
            while (inputs.TryDequeue(out var task))
            {
                graphicsContext.UploadTexture(task.texture, task.uploader);
                if (task.pack != null)
                    task.pack.Status = GraphicsObjectStatus.loaded;
                Output.Add(task);
            }
        }

        public int Count { get => inputs.Count + Output.Count; }

        ConcurrentQueue<TextureLoadTask> inputs = new();

        public List<TextureLoadTask> Output { get; } = new();

    }
}
