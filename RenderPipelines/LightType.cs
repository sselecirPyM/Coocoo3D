using Caprice.Display;
namespace RenderPipelines;

public enum LightType
{
    [UIShow(UIShowType.All, "方向光")]
    Directional,
    [UIShow(UIShowType.All, "点光")]
    Point,
}
