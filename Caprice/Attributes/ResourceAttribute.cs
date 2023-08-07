using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class ResourceAttribute : Attribute
    {
        public string Resource { get; }

        public ResourceAttribute(string resource)
        {
            this.Resource = resource;
        }
    }
}
