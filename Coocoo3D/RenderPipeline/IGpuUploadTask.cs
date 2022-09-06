using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public interface IGpuUploadTask : INavigableTask
    {
        Texture2D Texture { get; }
        Uploader Uploader { get; }
    }
}
