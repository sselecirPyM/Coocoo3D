using Caprice.Display;
using System.Numerics;

namespace RenderPipelines.MaterialDefines;


public class LightMaterial
{
    [UIColor(UIShowType.Light, "光照颜色")]
    public Vector3 LightColor = new Vector3(3, 3, 3);
    [UIDragFloat(0.1f, 0.1f, float.MaxValue, UIShowType.Light, "光照范围")]
    public float LightRange = 5.0f;

    [UIShow(UIShowType.Light, "光照类型")]
    public LightType LightType;
}