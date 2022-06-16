using Caprice.Display;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class VisualComponent : Component
    {
        public UIShowType UIShowType;
        public RenderMaterial material = new RenderMaterial();
        public Transform transform;

        public int bindId;
        public string bindBone;
        public bool bindX = true;
        public bool bindY = true;
        public bool bindZ = true;
        public bool bindRot;

        public VisualComponent GetClone()
        {
            var decal = (VisualComponent)MemberwiseClone();
            decal.material = material.GetClone();
            return decal;
        }
    }
}
