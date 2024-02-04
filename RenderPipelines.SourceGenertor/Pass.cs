using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public abstract class PassLike : PassObject
    {
        public string name;
        public List<PassLike> x_children = new List<PassLike>();
    }

    public class PassPlaceHolder : PassLike
    {
        public string placeHolderType;
        public bool generateCode = false;
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

    public class TextureProperty : PassLike
    {
        [Quotes]
        public string size;
        public string format;
        public string aov;
        public bool autoClear;
    }
}
