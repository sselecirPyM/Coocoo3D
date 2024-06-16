using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using ImGuiNET;
using System.Numerics;
using System;
using Coocoo3D.UI;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[ExportMetadata("MenuItem", "Render Buffer")]
public class RenderBufferWindow : IWindow
{
    public UIRenderSystem uiRenderSystem;
    public EditorContext editorContext;

    public string Title { get => "Render Buffer"; }

    public void OnGUI()
    {
        var view = editorContext.currentChannel.renderPipelineView;
        if (view != null)
        {
            ShowRenderBuffers(view);
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
