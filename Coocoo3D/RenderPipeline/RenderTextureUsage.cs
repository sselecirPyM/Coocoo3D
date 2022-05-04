using Caprice.Attributes;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class RenderTextureUsage
    {
        public Texture2D texture2D;
        public TextureCube textureCube;
        public GPUBuffer gpuBuffer;

        public string name;

        public int width = 1;
        public int height = 1;
        public int depth = 1;
        public int mips = 1;

        public bool baked;

        public object bakeTag;

        public ResourceFormat resourceFormat;

        public SizeAttribute sizeAttribute;

        public AutoClearAttribute autoClearAttribute;

        public FormatAttribute formatAttribute;

        public RuntimeBakeAttribute runtimeBakeAttribute;

        public BakeDependencyAttribute bakeDependencyAttribute;

        public SrgbAttribute srgbAttribute;
    }
}
