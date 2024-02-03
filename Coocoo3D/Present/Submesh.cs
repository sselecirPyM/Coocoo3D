namespace Coocoo3D.Present;

public class Submesh
{
    public string Name;

    public int indexOffset;
    public int indexCount;
    public int vertexStart;
    public int vertexCount;
    public bool DrawDoubleFace;

    public Vortice.Mathematics.BoundingBox boundingBox;
}
