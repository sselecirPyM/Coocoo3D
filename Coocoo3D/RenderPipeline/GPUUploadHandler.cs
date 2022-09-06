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
    public class GPUUploadHandler : IHandler<IGpuUploadTask>
    {
        public GraphicsContext graphicsContext;

        public bool Add(IGpuUploadTask task)
        {
            inputs.Enqueue(task);
            return true;
        }

        public void Update()
        {
            while (inputs.TryDequeue(out var task))
            {
                graphicsContext.UploadTexture(task.Texture, task.Uploader);
                Output.Add(task);
            }
        }

        public int Count { get => inputs.Count + Output.Count; }

        ConcurrentQueue<IGpuUploadTask> inputs = new();

        public List<IGpuUploadTask> Output { get; } = new();

    }
}
