using System.Numerics;

namespace RenderPipelines;

public struct PointLightData
{
    public Vector3 Position;
    public int unused;
    public Vector3 Color;
    public float Range;
}
