using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class DecalComponent : Component
    {
        public RenderMaterial material = new RenderMaterial();
        public Transform transform;
    }
}
