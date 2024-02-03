using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class FormatAttribute : Attribute
    {
        public ResourceFormat format;

        public FormatAttribute(ResourceFormat format)
        {
            this.format = format;
        }
    }
}
