using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Numerics;

namespace RenderPipelines;

public struct MeshRenderable
{
    public Mesh mesh;
    public int indexStart;
    public int indexCount;
    public int vertexStart;
    public int vertexCount;
    public RenderMaterial material;
    public Matrix4x4 transform;
    public bool drawDoubleFace;

    public const string POSITION = "POSITION0";
    public const string NORMAL = "NORMAL0";
    public const string TEXCOORD = "TEXCOORD0";
    public const string TANGENT = "TANGENT0";
}

public struct MeshRenderable<T>
{
    public Mesh mesh;
    public int indexStart;
    public int indexCount;
    public int vertexStart;
    public int vertexCount;
    public T material;
    public Matrix4x4 transform;
    public bool drawDoubleFace;
}
