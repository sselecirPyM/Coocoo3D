using System;
namespace Caprice.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class SizeAttribute : Attribute
{
    public int X { get; } = 1;
    public int Y { get; } = 1;
    public int Z { get; } = 1;
    public int Mips { get; } = 1;
    public string Source { get; }
    public int ArraySize { get; } = 1;

    public SizeAttribute(int width, int height = 1, int mips = 1, int arraySize = 1)
    {
        X = width;
        Y = height;
        Mips = mips;
        ArraySize = arraySize;
    }

    public SizeAttribute(string source)
    {
        this.Source = source;
    }
}
