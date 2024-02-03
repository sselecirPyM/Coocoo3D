using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public abstract class PassObject
    {
        public Dictionary<string, string> bindings = new Dictionary<string, string>();
        public Dictionary<string, string> directives = new Dictionary<string, string>();
    }
}
