using System.Collections.Generic;

namespace RenderPipelines.SourceGenertor
{
    internal class BindingsMap
    {
        Dictionary<string, List<string>> propertyBindings = new Dictionary<string, List<string>>();

        public void Add(string key, string value)
        {
            if (!propertyBindings.TryGetValue(key, out var list))
            {
                propertyBindings[key] = list = new List<string>();
            }
            list.Add(value);
        }

        public List<string> Get(string key)
        {
            propertyBindings.TryGetValue(key, out var list);
            return list;
        }

        public bool TryGetValue(string key, out List<string> value)
        {
            return propertyBindings.TryGetValue(key, out value);
        }
    }
}
