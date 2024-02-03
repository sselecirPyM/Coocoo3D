using Caprice.Display;
using Coocoo3D.Present;

namespace Coocoo3D.Components;

public class VisualComponent
{
    public UIShowType UIShowType;
    public RenderMaterial material = new RenderMaterial();
    public Transform transform;
    public int id;

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
