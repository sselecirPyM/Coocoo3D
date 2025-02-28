using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[ExportMetadata("MenuItem", "ImGui Demo")]
public class ImGUIDemoWindow : IWindow
{
    public EditorContext editorContext;
    public void OnGUI()
    {
        bool show = true;
        ImGui.ShowDemoWindow(ref show);
        if (!show)
        {
            editorContext.CloseWindow(this);
        }
    }

    public bool SimpleWindow { get => false; }
}
