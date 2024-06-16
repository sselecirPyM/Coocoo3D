using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Core;

public class EditorContext
{
    public HashSet<IWindow2> Windows2 = new HashSet<IWindow2>();
    public HashSet<IWindow> Windows = new HashSet<IWindow>();
    public HashSet<IWindow> RemovingWindow = new HashSet<IWindow>();
    public HashSet<IWindow> OpeningWindow = new HashSet<IWindow>();

    public bool showBoundingBox;

    public void OpenWindow(IWindow window)
    {
        OpeningWindow.Add(window);
    }
    public void CloseWindow(IWindow window)
    {
        RemovingWindow.Add(window);
    }

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
    public event Action<Entity> OnDeselectObject;

    public event Action<Entity> OnRemoveObject;

    public event Action<Vector2> OnMounseMoveDelta;

    public VisualChannel currentChannel;
}
