
namespace Coocoo3DGraphics;

public enum SlotResourceFlag
{
    None = 0,
    Linear = 1,
}

public class RayTracingShaderDescription
{
    public string name;

    public string anyHit;
    public string closestHit;
    public string intersection;
}
