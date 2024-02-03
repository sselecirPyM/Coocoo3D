using System;

namespace Caprice.Attributes
{
    public class SceneCaptureAttribute : Attribute
    {
        public string Capture { get; }

        public SceneCaptureAttribute(string capture)
        {
            this.Capture = capture;
        }
    }
}
