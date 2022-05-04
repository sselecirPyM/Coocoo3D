using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.UI.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class UISliderAttribute : UIShowAttribute
    {
        public float Min { get; }
        public float Max { get; }

        public UISliderAttribute(float min, float max, UIShowType type = UIShowType.Global, string name = null) : base(type, name)
        {
            this.Min = min;
            this.Max = max;
        }
    }
}
