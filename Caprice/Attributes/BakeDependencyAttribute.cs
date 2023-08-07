using System;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class BakeDependencyAttribute : Attribute
    {
        public string[] dependencies { get; }
        public BakeDependencyAttribute(params string[] dependencies)
        {
            this.dependencies = dependencies;
        }
    }
}
