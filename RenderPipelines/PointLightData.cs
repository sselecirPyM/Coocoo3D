using System.Numerics;

namespace RenderPipelines;

public struct PointLightData
{
    public Vector3 Position;
    public int unuse;
    public Vector3 Color;
    public float Range;
}

public class PointLightData1
{
    public Vector3 Position;
    public Vector3 Color;
    public float Range;

    public PointLightData GetPointLightData()
    {
        return new PointLightData
        {
            Color = Color,
            Position = Position,
            Range = Range,
        };
    }
}
