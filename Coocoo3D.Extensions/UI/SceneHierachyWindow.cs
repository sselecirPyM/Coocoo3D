using Coocoo3D.Components;
using Coocoo3D.Core;
using DefaultEcs;
using DefaultEcs.Command;
using ImGuiNET;
using System;
using System.ComponentModel.Composition;
using System.Numerics;

namespace Coocoo3D.UI;

[Export(typeof(IWindow))]
[Export(typeof(IEditorAccess))]
[ExportMetadata("MenuItem", "场景层级")]
public class SceneHierachyWindow : IWindow, IEditorAccess
{
    public Scene scene;

    public EntityCommandRecorder recorder;

    public EditorContext editorContext;
    public EngineContext engineContext;

    public string Title { get => "场景层级"; }
    public bool SimpleWindow { get => false; }

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
    }

    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    public void OnGUI()
    {
        if (ImGui.Begin("场景层级", ImGuiWindowFlags.MenuBar))
        {
            SceneHierarchy();
        }
        ImGui.End();
    }

    Entity selectedObject;
    void SceneHierarchy()
    {
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("场景命令"))
            {
                ImGuiExt.CommandMenu("UISceneCommand");
                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }
        string filter = ImGuiExt.ImFilter("查找物体", "查找名称");
        var gameObjects = scene.world;
        foreach (var gameObject in gameObjects)
        {
            TryGetComponent(gameObject, out ObjectDescription objectDescription);
            string name = objectDescription == null ? "object" : objectDescription.Name;

            if (!Contains(name, filter))
            {
                continue;
            }
            bool selected = gameObject == selectedObject;
            bool selecting = ImGui.Selectable(name + "###" + gameObject.GetHashCode(), ref selected);
            //if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
            //{
            //    int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
            //    if (n_next >= 0 && n_next < gameObjects.Count)
            //    {
            //        gameObjects[i] = gameObjects[n_next];
            //        gameObjects[n_next] = gameObject;
            //        ImGui.ResetMouseDragDelta();
            //    }
            //}
            if (selecting || !selectedObject.IsAlive)
            {
                editorContext.SelectObjectMessage(gameObject);
            }
        }
        if((ImGui.IsKeyPressed((int)ImGuiKey.Delete) && ImGui.IsWindowHovered()))
        {
            if (editorContext.selectedObject.IsAlive)
            {
                recorder.Record(editorContext.selectedObject).Dispose();
                editorContext.RemoveObjectMessage(editorContext.selectedObject);
            }
        }
    }


    static bool Contains(string input, string filter)
    {
        return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }


    static bool TryGetComponent<T>(Entity obj, out T value)
    {
        if (obj.Has<T>())
        {
            value = obj.Get<T>();
            return true;
        }
        else
        {
            value = default(T);
            return false;
        }
    }
}
