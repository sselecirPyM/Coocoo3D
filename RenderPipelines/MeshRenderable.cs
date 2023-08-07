using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines;

public struct MeshRenderable
{
    public Mesh mesh;
    public Mesh meshOverride;
    public int indexStart;
    public int indexCount;
    public int vertexStart;
    public int vertexCount;
    public RenderMaterial material;
    public Matrix4x4 transform;
    public bool gpuSkinning;
    public bool drawDoubleFace;
}

public class MeshRenderable1
{
    public Mesh mesh;
    public Mesh meshOverride;
    public int indexStart;
    public int indexCount;
    public int vertexStart;
    public int vertexCount;
    public Matrix4x4 transform;
    public bool gpuSkinning;
    public bool drawDoubleFace;

    public IDictionary<string, object> properties;

    public CBuffer boneBuffer;

    public MeshRenderable1 GetClone()
    {
        MeshRenderable1 newObj = (MeshRenderable1)MemberwiseClone();
        newObj.properties = new Dictionary<string, object>(properties);
        return newObj;
    }
}
