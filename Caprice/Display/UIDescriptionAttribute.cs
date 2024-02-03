using System;

namespace Caprice.Display;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class UIDescriptionAttribute : Attribute
{
    public string Description { get; }

    public UIDescriptionAttribute(string description)
    {
        this.Description = description;
    }
}
