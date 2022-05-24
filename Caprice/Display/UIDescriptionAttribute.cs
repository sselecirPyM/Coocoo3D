using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caprice.Display
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class UIDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public UIDescriptionAttribute(string description)
        {
            this.Description = description;
        }
    }
}
