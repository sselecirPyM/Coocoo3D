using Caprice.Display;
using Coocoo3DGraphics;
using System.Numerics;

namespace RenderPipelines.MaterialDefines;

public class DecalMaterial
{
    [UIShow(UIShowType.Decal, "启用贴花颜色")]
    public bool EnableDecalColor = true;
    [UIShow(UIShowType.Decal, "贴花颜色贴图")]
    public Texture2D DecalColorTexture;

    [UIShow(UIShowType.Decal, "启用贴花发光")]
    public bool EnableDecalEmissive;
    [UIShow(UIShowType.Decal, "贴花发光贴图")]
    public Texture2D DecalEmissiveTexture;
    [UIColor(UIShowType.Decal, "发光强度")]
    public Vector4 _DecalEmissivePower = new Vector4(1, 1, 1, 1);
}
