using System;

namespace Caprice.Display;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false)]
public class TextAttribute : Attribute
{
    public string Text { get; }
    public string Description { get; }

    public TextAttribute(string text, string description = null)
    {
        Text = text;
        Description = description;
    }
}
