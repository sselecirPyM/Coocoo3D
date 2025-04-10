using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.UI;

public class PopupsWindow : IWindow2
{
    public UIImGui uiImGui;

    public bool Removing { get; private set; }

    public EditorContext editorContext;

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
    }

    Entity selectedObject;
    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    public void OnGUI()
    {
        if (UIImGui.requestParamEdit)
        {
            UIImGui.requestParamEdit = false;
            ImGui.OpenPopup("编辑参数");
            popupParamEdit = true;
        }
        PopupsWindow1(selectedObject);
    }


    void PopupsWindow1(Entity gameObject)
    {
        if (UIImGui.requestOpenResource)
        {
            UIImGui.requestOpenResource = false;
            ImGui.OpenPopup("选择资源");
            popupOpenResource = true;
        }
        ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.Appearing);
        if (ImGui.BeginPopupModal("选择资源", ref popupOpenResource))
        {
            if (ImGui.Button("关闭"))
            {
                popupOpenResource = false;
            }
            var _open = ResourceWindow.Resources();
            if (_open != null)
            {
                UIImGui.fileOpenResult = _open.FullName;
                popupOpenResource = false;
            }
            ImGui.EndPopup();
        }
        if (ImGui.BeginPopupModal("编辑参数", ref popupParamEdit))
        {
            var matObj = editorContext.currentChannel.renderPipelineView.renderPipeline.UIMaterial(UIImGui.paramEdit);
            uiImGui.ShowParams(matObj, UIImGui.paramEdit.Parameters);

            if (ImGui.Button("确定"))
            {
                TryGetComponent<MeshRendererComponent>(gameObject, out var meshRenderer);
                TryGetComponent<MMDRendererComponent>(gameObject, out var mmdRenderer);
                IList<RenderMaterial> materials = null;
                if (meshRenderer != null)
                {
                    materials = meshRenderer.Materials;
                }
                if (mmdRenderer != null)
                {
                    materials = mmdRenderer.Materials;
                }
                foreach (var material in materials)
                    foreach (var param in UIImGui.paramEdit.Parameters)
                    {
                        material.Parameters[param.Key] = param.Value;
                    }

                UIImGui.paramEdit = null;

                popupParamEdit = false;
            }
            if (ImGui.Button("取消"))
            {
                popupParamEdit = false;
            }
            ImGui.EndPopup();
        }
    }

    bool popupParamEdit;
    bool popupOpenResource;

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
