using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public abstract class PassLike : PassObject
    {
        public string name;
        public List<PassLike> children = new List<PassLike>();
    }

    public class PassPlaceHolder : PassLike
    {
        public string type;
        public Dictionary<string, string> data = new Dictionary<string, string>();
    }

    public class CopyPass : PassLike
    {
        public string from;
        public string to;
    }

    public class DrawCall : PassLike
    {
        public string indexCount;
        public string indexStart;
        public string type;
    }

    public class DispatchCall : PassLike
    {
        public string x = "1";
        public string y = "1";
        public string z = "1";
        public string type;
    }

    public class Pass : PassLike
    {

    }

    public class Script : PassLike
    {
        public string source;
    }
}
