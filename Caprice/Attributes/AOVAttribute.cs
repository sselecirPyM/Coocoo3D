using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
