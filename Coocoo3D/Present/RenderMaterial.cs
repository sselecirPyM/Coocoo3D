﻿using System.Collections.Generic;

namespace Coocoo3D.Present;

public class RenderMaterial
{
    public string Name;

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
