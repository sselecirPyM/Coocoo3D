using Caprice.Attributes;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System.Numerics;
using System.Reflection;

namespace Coocoo3D.RenderPipeline;

public class RenderTextureUsage
{
    public GPUBuffer gpuBuffer;

    public object bindingObject;

    public string name;

    public int width = 1;
    public int height = 1;
    public int depth = 1;
    public int mips = 1;
    public int arraySize = 1;

    public bool ready;

    public object bakeTag;

    public ResourceFormat resourceFormat;

    public string sizeSource;


    public bool autoClear;
    public Vector4 autoClearColor;
    public float autoClearDepth = 1.0f;

    public RuntimeBakeAttribute runtimeBakeAttribute;

    public string[] bakeDependency;

    public MemberInfo memberInfo;

    public Texture2D texture;

    public Texture2D GetTexture2D()
    {
        if (texture != null)
            return texture;
        if (gpuBuffer != null)
            return null;
        if (memberInfo.GetGetterType() != typeof(Texture2D))
        {
            return null;
        }
        return memberInfo.GetValue2<Texture2D>(bindingObject);
    }
}
