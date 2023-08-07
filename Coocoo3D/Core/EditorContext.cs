using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Core;

public class EditorContext
{
    public HashSet<IWindow> Windows = new();

    public void SelectObjectMessage(Entity entity)
    {
        OnSelectObject?.Invoke(entity);
    }

    public void RemoveObjectMessage(Entity entity)
    {
        OnRemoveObject?.Invoke(entity);
    }

    public void MouseMoveDeltaMessage(Vector2 delta)
    {
        OnMounseMoveDelta?.Invoke(delta);
    }

    public event Action<Entity> OnSelectObject;
    public event Action<Entity> OnRemoveObject;

    public event Action<Vector2> OnMounseMoveDelta;

    public VisualChannel currentChannel;
}
