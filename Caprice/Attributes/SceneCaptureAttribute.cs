using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caprice.Attributes
{
    public class SceneCaptureAttribute : Attribute
    {
        public string Capture { get; }

        public SceneCaptureAttribute()
        {

        }

        public SceneCaptureAttribute(string capture)
        {
            this.Capture = capture;
        }
    }
}
