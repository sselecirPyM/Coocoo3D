using System;

namespace Caprice.Display;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class UIShowAttribute : Attribute
{
    public string Name { get; }

    public UIShowType Type { get; }

    public UIShowAttribute(UIShowType type = UIShowType.Global, string name = null)
    {
        this.Name = name;
        this.Type = type;
    }
}
public enum UIShowType
{
    Unknown = 0,
    Global = 1,
    Material = 2,
    Decal = 4,
    Light = 8,
    Particle = 16,
}
