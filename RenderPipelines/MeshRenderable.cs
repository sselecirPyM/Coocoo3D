using Coocoo3D.Present;
using Coocoo3DGraphics;
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
