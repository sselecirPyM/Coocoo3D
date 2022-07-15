using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caprice.Display
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false)]
    public class UIShowAttribute : Attribute
    {
        public string Name { get; }

        public UIShowType Type { get; }

        public UIShowAttribute(UIShowType type = UIShowType.Global, string name = null)
        {
            this.Name = name;
            this.Type = type;
        }
    }
    public enum UIShowType
    {
        All = 0,
        Global = 1,
        Material = 2,
        Decal = 4,
        Light = 8,
        Particle = 16,
    }
}
