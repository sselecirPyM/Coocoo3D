using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using ImGuiNET;
using System.Numerics;
using System;

namespace Coocoo3D.UI;

public class RenderBufferWindow : IWindow
{
    public bool Removing { get; private set; }

    public UIRenderSystem uiRenderSystem;
    public EditorContext editorContext;

    bool show = true;

    public void OnGui()
    {
        if (ImGui.Begin("buffers", ref show))
        {
            var view = editorContext.currentChannel.renderPipelineView;
            if (view != null)
            {
                ShowRenderBuffers(view);
            }
        }
        ImGui.End();
        if (!show)
        {
            Removing = true;
        }
    }

    void ShowRenderBuffers(RenderPipelineView view)
    {
        string filter = ImGuiExt.ImFilter("filter", "filter");
        foreach (var pair in view.RenderTextures)
        {
            var tex2D = pair.Value.GetTexture2D();
            if (tex2D == null)
                continue;
            if (!Contains(pair.Key, filter))
                continue;
            IntPtr imageId = uiRenderSystem.ShowTexture(tex2D);

            ImGui.TextUnformatted(pair.Key);
            ImGui.Image(imageId, new Vector2(150, 150));

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tex2D.GetFormat().ToString());
                ImGui.TextUnformatted(string.Format("width:{0} height:{1}", tex2D.width, tex2D.height));
                ImGui.Image(imageId, new Vector2(384, 384));
                ImGui.EndTooltip();
            }
        }
    }


    static bool Contains(string input, string filter)
    {
        return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }
}
