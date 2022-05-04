using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class IndexableAttribute : Attribute
    {
        public string Name { get; }
        public IndexableAttribute()
        {

        }
        public IndexableAttribute(string name)
        {
            Name = name;
        }
    }
}
