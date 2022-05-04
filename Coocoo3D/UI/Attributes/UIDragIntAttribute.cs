using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.UI.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class UIDragIntAttribute : UIShowAttribute
    {
        public int Min { get; } = int.MinValue;
        public int Max { get; } = int.MaxValue;

        public int Step { get; } = 1;

        public UIDragIntAttribute(int step, int min = int.MinValue, int max = int.MaxValue, UIShowType type = UIShowType.Global, string name = null) : base(type, name)
        {
            this.Min = min;
            this.Max = max;
            this.Step = step;
        }
    }
}
