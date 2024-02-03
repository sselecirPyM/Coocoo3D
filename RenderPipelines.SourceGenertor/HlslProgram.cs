using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public class HlslProgram : PassObject
    {
        public string name;
        public List<HlslVar> vars = new List<HlslVar>();
        public List<HlslVar> srvs = new List<HlslVar>();
        public List<HlslVar> uavs = new List<HlslVar>();
        public List<HlslSampler> samplers = new List<HlslSampler>();

        [Quotes]
        public string vertex;
        [Quotes]
        public string geometry;
        [Quotes]
        public string pixel;
        [Quotes]
        public string compute;

        public string code;
    }
    public class HlslVar : PassObject
    {
        public string name;
        public string type;
        public string xType;
        public string value;
        public string arraySize;
        public bool autoBinding = true;
    }

    public class HlslSampler : PassObject
    {
        public string name;
        public string type;
        public string addressU;
        public string addressV;
        public string mode;
    }

    public class ShaderVariantCollection : PassObject
    {
        public string name;
        public List<ShaderVariant> variants = new List<ShaderVariant>();
    }

    public class ShaderVariant : PassObject
    {
        public string keyword;
    }

    public class GeneratedHlsl
    {
        public string name;
        public string code;
    }

}
