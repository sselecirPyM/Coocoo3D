using Caprice.Attributes;
using Coocoo3DGraphics;
using System.Reflection;

namespace Coocoo3D.RenderPipeline;

public class RenderTextureUsage
{
    public GPUBuffer gpuBuffer;

    public RenderPipeline renderPipeline;

    public string name;

    public int width = 1;
    public int height = 1;
    public int depth = 1;
    public int mips = 1;
    public int arraySize = 1;

    public bool ready;

    public object bakeTag;

    public ResourceFormat resourceFormat;

    public SizeAttribute sizeAttribute;

    public AutoClearAttribute autoClearAttribute;

    public FormatAttribute formatAttribute;

    public RuntimeBakeAttribute runtimeBakeAttribute;

    public BakeDependencyAttribute bakeDependencyAttribute;

    public FieldInfo fieldInfo;

    public Texture2D GetTexture2D()
    {
        if (fieldInfo.FieldType != typeof(Texture2D))
        {
            return null;
        }
        return fieldInfo.GetValue(renderPipeline) as Texture2D;
    }
}
