using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using System.Collections.Generic;

namespace Coocoo3D.Components;

public class MeshRendererComponent
{
    public string meshPath;
    public ModelPack model;
    public List<RenderMaterial> Materials = new();
    public Transform transform;

    public MeshRendererComponent GetClone()
    {
        var clone = (MeshRendererComponent)MemberwiseClone();
        clone.Materials = new List<RenderMaterial>(Materials.Count);
        foreach (var mat in Materials)
            clone.Materials.Add(mat.GetClone());
        return clone;
    }
}
