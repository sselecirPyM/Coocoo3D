using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    public class Component
    {
        public string name;
        public bool generateDispose = true;
        public List<ComponentProperty> properties = new List<ComponentProperty>();
        public List<HlslProgram> hlslPrograms = new List<HlslProgram>();
        public List<PassLike> children = new List<PassLike>();
        public List<ShaderVariantCollection> variantCollections = new List<ShaderVariantCollection>();
        public List<ComponentUsing> usings = new List<ComponentUsing>();
    }

    public class ComponentProperty
    {
        public string name;
        public string type;
    }

    public class ComponentUsing
    {
        public string value;
    }
}
