using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using DefaultEcs;
using DefaultEcs.Command;
using ImGuiNET;
using System;
using System.Numerics;

namespace Coocoo3D.UI;

public class SceneHierachyWindow : IWindow
{
    public bool Removing { get; private set; }
    public Scene CurrentScene;

    public EntityCommandRecorder recorder;

    public EditorContext editorContext;

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
    }

    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    public void OnGui()
    {
        ImGui.SetNextWindowPos(new Vector2(750, 0), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("场景层级"))
        {
            SceneHierarchy();
        }
        ImGui.End();
    }

    Entity selectedObject;
    void SceneHierarchy()
    {
        if (ImGui.Button("新光源"))
        {
            CurrentScene.NewLighting();
        }
        ImGui.SameLine();
        if (ImGui.Button("新粒子"))
        {
            NewParticle();
        }
        ImGui.SameLine();
        if (ImGui.Button("新贴花"))
        {
            CurrentScene.NewDecal();
        }
        //ImGui.SameLine();
        bool removeObject = false;
        if (ImGui.Button("移除物体") || (ImGui.IsKeyPressed((int)ImGuiKey.Delete) && ImGui.IsWindowHovered()))
        {
            removeObject = true;
        }
        bool copyObject = false;
        ImGui.SameLine();
        if (ImGui.Button("复制物体"))
        {
            copyObject = true;
        }
        string filter = ImGuiExt.ImFilter("查找物体", "查找名称");
        var gameObjects = CurrentScene.world;
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

                //CurrentScene.SelectedGameObjects.Clear();
                //CurrentScene.SelectedGameObjects.Add(gameObject.GetHashCode());
            }
        }
        if (removeObject)
        {
            if (selectedObject.IsAlive)
                recorder.Record(selectedObject).Dispose();

        }
        if (copyObject)
        {
            if (selectedObject.IsAlive)
                CurrentScene.DuplicateObject(selectedObject);
        }
    }


    void NewParticle()
    {
        var world = CurrentScene.recorder.Record(CurrentScene.world);
        var gameObject = world.CreateEntity();
        VisualComponent component = new VisualComponent();
        component.UIShowType = UIShowType.Particle;
        gameObject.Set(component);
        gameObject.Set(new ObjectDescription
        {
            Name = "粒子",
            Description = ""
        });
        gameObject.Set(new Transform(new Vector3(0, 0, 0), Quaternion.CreateFromYawPitchRoll(0, 0, 0), new Vector3(1, 1, 1)));
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
