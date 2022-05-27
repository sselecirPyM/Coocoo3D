using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MeshRendererComponent : Component
    {
        public string meshPath;
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
}
