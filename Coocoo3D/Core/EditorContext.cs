using Arch.Core;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace Coocoo3D.Core;

public class EditorContext
{
    public EngineContext engineContext;

    public HashSet<IWindow2> Windows2 = new HashSet<IWindow2>();
    public HashSet<IWindow> Windows = new HashSet<IWindow>();
    public HashSet<IWindow> RemovingWindow = new HashSet<IWindow>();
    public HashSet<IWindow> OpeningWindow = new HashSet<IWindow>();

    public bool showBoundingBox;
    string openWindowConfigFile = Path.GetFullPath("EditorConfig.json", Path.GetDirectoryName(Environment.ProcessPath));

    public void ReOpenWindows()
    {
        try
        {
            var config = JsonConvert.DeserializeObject<EditorConfig>(File.ReadAllText(openWindowConfigFile));
            foreach (var t in engineContext.extensionFactory.Windows)
            {
                if (config.OpenWindows.Contains(t.Metadata.MenuItem))
                {
                    OpenWindow(t.Value);
                }
            }
        }
        catch
        {

        }
    }

    public void SaveOpenWindow()
    {
        try
        {
            EditorConfig editorConfig = new EditorConfig();
            editorConfig.OpenWindows = new HashSet<string>();
            foreach (var window in Windows)
            {
                foreach (var attr in window.GetType().GetCustomAttributes<ExportMetadataAttribute>())
                {
                    if (attr.Name == "MenuItem")
                    {
                        editorConfig.OpenWindows.Add(attr.Value.ToString());
                        break;
                    }
                }
            }
            File.WriteAllText(openWindowConfigFile, JsonConvert.SerializeObject(editorConfig));
        }
        catch
        {

        }
    }

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
        selectedObject = entity;
    }

    public void RemoveObjectMessage(Entity entity)
    {
        OnRemoveObject?.Invoke(entity);
    }

    public void MouseMoveDeltaMessage(Vector2 delta)
    {
        OnMounseMoveDelta?.Invoke(delta);
    }

    public Entity selectedObject;

    public event Action<Entity> OnSelectObject;
    public event Action<Entity> OnDeselectObject;

    public event Action<Entity> OnRemoveObject;

    public event Action<Vector2> OnMounseMoveDelta;

    public VisualChannel currentChannel;
}

public class EditorConfig
{
    public HashSet<string> OpenWindows;
}