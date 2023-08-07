using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class AutoClearAttribute : Attribute
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public float Depth { get; } = 1.0f;
        public byte Stencil { get; }

        public AutoClearAttribute()
        {

        }

        public AutoClearAttribute(float r, float g, float b, float a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        public AutoClearAttribute(float depth, byte stencil)
        {
            this.Depth = depth;
            this.Stencil = stencil;
        }
    }
}
