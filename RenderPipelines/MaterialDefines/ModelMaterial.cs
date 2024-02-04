using Caprice.Display;
using Coocoo3DGraphics;

namespace RenderPipelines.MaterialDefines;

public class ModelMaterial
{
    [UIShow(UIShowType.Material, "透明材质")]
    public bool IsTransparent;

    [UISlider(0.0f, 1.0f, UIShowType.Material, "金属")]
    public float Metallic;

    [UISlider(0.0f, 1.0f, UIShowType.Material, "粗糙")]
    public float Roughness = 0.8f;

    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Material, "发光")]
    public float Emissive;

    [UISlider(0.0f, 1.0f, UIShowType.Material, "高光")]
    public float Specular = 0.5f;

    [UISlider(0.0f, 1.0f, UIShowType.Material, "遮蔽")]
    public float AO = 1.0f;

    [UIShow(UIShowType.Material)]
    public Texture2D _Albedo;

    [UIShow(UIShowType.Material)]
    public Texture2D _Metallic;

    [UIShow(UIShowType.Material)]
    public Texture2D _Roughness;

    [UIShow(UIShowType.Material)]
    public Texture2D _Emissive;

    [UIShow(UIShowType.Material, "使用法线贴图")]
    public bool UseNormalMap;

    [UIShow(UIShowType.Material)]
    public Texture2D _Normal;

    [UIShow(UIShowType.Material, "使用Spa")]
    public bool UseSpa;

    [UIShow(UIShowType.Material)]
    public Texture2D _Spa;
}
