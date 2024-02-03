using System.Collections.Generic;

namespace Coocoo3D.Present;

public class RenderMaterial
{
    public string Name;

    public RenderMaterial baseMaterial;

    public object GetObject(string key)
    {
        if (Parameters.TryGetValue(key, out object obj))
            return obj;
        return baseMaterial?.GetObject(key);
    }

    public T GetObject<T>(string key)
    {
        if (Parameters.TryGetValue(key, out object obj) && obj is T res)
        {
            return res;
        }
        if (baseMaterial == null)
        {
            return default;
        }
        return baseMaterial.GetObject<T>(key);
    }

    public Dictionary<string, object> Parameters = new Dictionary<string, object>();

    public RenderMaterial GetClone()
    {
        var mat = (RenderMaterial)MemberwiseClone();
        mat.Parameters = new Dictionary<string, object>(Parameters);
        return mat;
    }

    public override string ToString()
    {
        return Name;
    }
}
