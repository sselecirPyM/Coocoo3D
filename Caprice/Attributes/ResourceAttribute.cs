using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
