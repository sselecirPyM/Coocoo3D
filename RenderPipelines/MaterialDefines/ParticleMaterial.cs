using Caprice.Display;
using Coocoo3DGraphics;
using System;
using System.Numerics;

namespace RenderPipelines.MaterialDefines;

public class ParticleMaterial : ICloneable
{
    [UIDragInt(1, 0, 1000, UIShowType.Particle, "最大数量")]
    public int MaxCount = 1000;
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "随机速度")]
    public Vector2 RandomSpeed = new Vector2(-0.5f, 0.5f);
    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "初始速度")]
    public Vector3 InitialSpeed;
    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "尺寸")]
    public Vector2 Scale = new Vector2(0.02f, 0.02f);
    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "加速度")]
    public Vector3 Acceleration;

    [UIDragFloat(0.1f, type: UIShowType.Particle, name: "产生速度")]
    public float GenerateSpeed = 2;

    [UIDragFloat(0.05f, type: UIShowType.Particle, name: "移动时产生")]
    public float MoveToGenerate = 10.0f;

    [UIDragInt(1, 0, 1000, UIShowType.Particle, "产生数量")]
    public int GenerateCount = 1;

    [UIDragFloat(0.01f, 0, float.MaxValue, UIShowType.Particle, "生命")]
    public Vector2 Life = new Vector2(1.0f, 1.5f);

    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "速度到生命")]
    public float LifeBySpeed = -0.5f;

    [UIDragFloat(0.01f, float.MinValue, float.MaxValue, UIShowType.Particle, "速度到生命最大值")]
    public float LifeBySpeedMax = 1.0f;

    [UIShow(UIShowType.Particle, "贴图")]
    public Texture2D ParticleTexture;
    [UIColor(UIShowType.Particle, "颜色")]
    public Vector4 Color = new Vector4(1, 1, 1, 1);
    [UIShow(UIShowType.Particle, "混合模式")]
    public BlendMode BlendMode;



    public object Clone()
    {
        return MemberwiseClone();
    }
}
