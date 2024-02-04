using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using DefaultEcs;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.UI;

public class GameObjectWindow : IWindow
{
    public GameDriverContext gameDriverContext;

    public Scene scene;

    public UIImGui uiImGui;

    public EditorContext editorContext;

    public bool Removing { get; private set; }

    int materialSelectIndex = 0;

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
    }

    Entity selectedObject;
    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    public void OnGui()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 400), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("物体"))
        {
            if (selectedObject.IsAlive)
            {
                GameObjectPanel(selectedObject);
            }
        }
        ImGui.End();
    }


    void GameObjectPanel(Entity gameObject)
    {
        TryGetComponent<MMDRendererComponent>(gameObject, out var renderer);
        TryGetComponent<MeshRendererComponent>(gameObject, out var meshRenderer);
        TryGetComponent<VisualComponent>(gameObject, out var visual);
        TryGetComponent<AnimationStateComponent>(gameObject, out var animationState);
        TryGetComponent<ObjectDescription>(gameObject, out var objectDescription);

        ImGui.InputText("名称", ref objectDescription.Name, 256);
        if (ImGui.TreeNode("描述"))
        {
            ImGui.Text(objectDescription.Description);
            if (renderer != null)
            {
                var mesh = renderer.model.GetMesh();
                ImGui.Text(string.Format("顶点数：{0} 索引数：{1} 材质数：{2}\n模型文件：{3}\n",
                    mesh.GetVertexCount(), mesh.GetIndexCount(), renderer.Materials.Count,
                    renderer.meshPath));
            }

            ImGui.TreePop();
        }
        if (ImGui.TreeNode("变换"))
        {
            Vector3 position = UIImGui.position;
            if (ImGui.DragFloat3("位置", ref position, 0.01f))
            {
                UIImGui.position = position;
                UIImGui.positionChange = true;
            }
            Vector3 rotation = UIImGui.rotation / MathF.PI * 180;
            if (ImGui.DragFloat3("旋转", ref rotation))
            {
                UIImGui.rotation = rotation * MathF.PI / 180;
                UIImGui.rotationChange = true;
            }
            ImGui.TreePop();
        }
        if (renderer != null)
        {
            RendererComponent(renderer, animationState);
        }
        if (meshRenderer != null)
        {
            RendererComponent(meshRenderer);
        }
        if (visual != null)
        {
            VisualComponent(ref gameObject.Get<Transform>(), visual);
        }
    }

    void RendererComponent(MMDRendererComponent renderer, AnimationStateComponent animationState)
    {
        if (ImGui.TreeNode("材质"))
        {
            ShowMaterials(renderer.model.Submeshes, renderer.Materials);
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("动画"))
        {
            ImGui.Text(string.Format("动作文件：{0}", animationState.motionPath));
            if (ImGui.Button("清除动画"))
            {
                gameDriverContext.RefreshScene = true;
                animationState.motionPath = "";
            }
            ImGui.Checkbox("蒙皮", ref renderer.skinning);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("关闭蒙皮可以提高性能");
            ImGui.Checkbox("锁定动作", ref animationState.LockMotion);
            if (animationState.LockMotion)
            {
                string filter = ImGuiExt.ImFilter("搜索变形", "搜索变形");
                for (int i = 0; i < renderer.Morphs.Count; i++)
                {
                    MorphDesc morpth = renderer.Morphs[i];
                    if (!Contains(morpth.Name, filter))
                        continue;
                    if (ImGui.SliderFloat(morpth.Name, ref animationState.Weights.Origin[i], 0, 1))
                    {
                        gameDriverContext.RefreshScene = true;
                    }
                }
            }
            ImGui.TreePop();
        }
    }

    void RendererComponent(MeshRendererComponent renderer)
    {
        if (ImGui.TreeNode("材质"))
        {
            ShowMaterials(renderer.model.Submeshes, renderer.Materials);
            ImGui.TreePop();
        }
    }

    void ShowMaterials(List<Submesh> submeshes, List<RenderMaterial> materials)
    {
        if (ImGui.BeginChild("materials", new Vector2(120, 400)))
        {
            ImGui.PushItemWidth(120);
            for (int i = 0; i < materials.Count; i++)
            {
                var submesh = submeshes[i];
                bool selected = i == materialSelectIndex;
                ImGui.Selectable(string.Format("{0}##{1}", submesh.Name, i), ref selected);
                if (selected) materialSelectIndex = i;
            }
            ImGui.PopItemWidth();
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("materialProperty", new Vector2(200, 400)))
        {
            if (materialSelectIndex >= 0 && materialSelectIndex < materials.Count)
            {
                var material = materials[materialSelectIndex];
                var submesh = submeshes[materialSelectIndex];
                ImGui.Text(submesh.Name);
                if (ImGui.Button("修改此物体所有材质"))
                {
                    UIImGui.StartEditParam();
                }
                material.Type = UIShowType.Material;
                var matObj = editorContext.currentChannel.renderPipelineView.renderPipeline.UIMaterial(material);
                uiImGui.ShowParams(matObj, material.Parameters);
            }
        }
        ImGui.EndChild();
    }

    void VisualComponent(ref Transform transform, VisualComponent visualComponent)
    {
        if (ImGui.TreeNode("绑定"))
        {
            int rendererCount = scene.renderers.Count;
            string[] renderers = new string[rendererCount + 1];
            int[] ids = new int[rendererCount + 1];
            renderers[0] = "-";
            ids[0] = -1;
            int count = 1;
            int currentItem = 0;

            foreach (var gameObject1 in scene.world)
            {
                if (!TryGetComponent<MMDRendererComponent>(gameObject1, out var renderer))
                    continue;
                TryGetComponent<ObjectDescription>(gameObject1, out var desc);
                renderers[count] = desc == null ? "object" : desc.Name;
                ids[count] = gameObject1.GetHashCode();
                if (gameObject1.GetHashCode() == visualComponent.bindId)
                    currentItem = count;
                count++;
                if (count == rendererCount + 1)
                    break;
            }
            if (ImGui.Combo("绑定到物体", ref currentItem, renderers, rendererCount + 1))
            {
                visualComponent.bindId = ids[currentItem];
            }

            string[] bones;
            if (scene.gameObjects.TryGetValue(visualComponent.bindId, out var gameObject2))
            {
                var renderer = gameObject2.Get<MMDRendererComponent>();
                bones = new string[renderer.bones.Count + 1];
                bones[0] = "-";
                for (int i = 0; i < renderer.bones.Count; i++)
                {
                    bones[i + 1] = renderer.bones[i].Name;
                }
            }
            else
            {
                bones = new string[0];
            }
            currentItem = 0;
            for (int i = 1; i < bones.Length; i++)
            {
                if (bones[i] == visualComponent.bindBone)
                    currentItem = i;
            }
            if (ImGui.Combo("绑定骨骼", ref currentItem, bones, bones.Length))
            {
                if (currentItem > 0)
                {
                    visualComponent.bindBone = bones[currentItem];
                }
                else
                {
                    visualComponent.bindBone = null;
                }
            }
            ImGui.Checkbox("绑定X", ref visualComponent.bindX);
            ImGui.Checkbox("绑定Y", ref visualComponent.bindY);
            ImGui.Checkbox("绑定Z", ref visualComponent.bindZ);
            ImGui.Checkbox("绑定旋转", ref visualComponent.bindRot);
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("视觉"))
        {
            var renderPipelineView = editorContext.currentChannel.renderPipelineView;
            ImGui.Checkbox("显示包围盒", ref UIImGui.showBounding);

            ImGui.DragFloat3("大小", ref transform.scale, 0.01f);

            var obj = renderPipelineView.renderPipeline.UIMaterial(visualComponent.material);
            if (obj != null)
                uiImGui.ShowParams(obj, visualComponent.material.Parameters);
            ImGui.TreePop();
        }
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

    static bool Contains(string input, string filter)
    {
        return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }
}
