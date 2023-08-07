using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public abstract class RuntimeBakeAttribute : Attribute
    {

    }
}
