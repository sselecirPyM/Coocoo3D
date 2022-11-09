using Caprice.Attributes;
using Coocoo3DGraphics;
using System.Reflection;

namespace Coocoo3D.RenderPipeline
{
    public class RenderTextureUsage
    {
        public Texture2D texture2D;
        public GPUBuffer gpuBuffer;

        public string name;

        public int width = 1;
        public int height = 1;
        public int depth = 1;
        public int mips = 1;
        public int arraySize = 1;

        public bool baked;

        public object bakeTag;

        public ResourceFormat resourceFormat;

        public SizeAttribute sizeAttribute;

        public AutoClearAttribute autoClearAttribute;

        public FormatAttribute formatAttribute;

        public RuntimeBakeAttribute runtimeBakeAttribute;

        public BakeDependencyAttribute bakeDependencyAttribute;

        public SrgbAttribute srgbAttribute;

        public FieldInfo fieldInfo;
    }
}
