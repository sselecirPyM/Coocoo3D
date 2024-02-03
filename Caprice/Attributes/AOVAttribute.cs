using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class AOVAttribute : Attribute
    {
        public AOVType AOVType { get; }

        public AOVAttribute(AOVType aovType)
        {
            AOVType = aovType;
        }
    }
}
