using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public class Render : PassLike
    {
        public string type;
        public string parameter;
        public RenderBufferBind indexBuffer;
        public List<RenderBufferBind> vertexBuffers = new List<RenderBufferBind>();
        public List<HlslVar> vars = new List<HlslVar>();
        public List<HlslVar> srvs = new List<HlslVar>();
        public List<HlslVar> uavs = new List<HlslVar>();
        public Blend blend;
    }

    public class RenderShader : PassLike
    {
        public string source;
    }

    public class RenderBufferBind : PassObject
    {
        public string name;
        public string value;
    }

    public class Blend : PassObject
    {
        public string src;
        public string dst;
    }
}
