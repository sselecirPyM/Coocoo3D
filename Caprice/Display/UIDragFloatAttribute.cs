using System;

namespace Caprice.Display;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class UIDragFloatAttribute : UIShowAttribute
{
    public float Min { get; } = float.MinValue;
    public float Max { get; } = float.MaxValue;

    public float Step { get; } = 1;

    public UIDragFloatAttribute(float step, float min = float.MinValue, float max = float.MaxValue, UIShowType type = UIShowType.Global, string name = null) : base(type, name)
    {
        this.Min = min;
        this.Max = max;
        this.Step = step;
    }
}
