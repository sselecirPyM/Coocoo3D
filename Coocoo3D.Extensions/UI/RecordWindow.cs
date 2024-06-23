using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[Export(typeof(IEditorAccess))]
[ExportMetadata("MenuItem", "录制")]
public class RecordWindow : IWindow, IEditorAccess
{
    public string Title { get => "录制"; }

    public RecordSystem recordSystem;

    public void OnGUI()
    {
        if (recordSystem.ffmpegInstalled)
        {
            ImGui.Text("已安装FFmpeg。输出文件名output.mp4");
            ImGui.Checkbox("使用FFmpeg输出mp4", ref recordSystem.useFFmpeg);
        }
        var recordSettings = recordSystem.recordSettings;
        UIImGui.ShowObject(recordSettings);

        if (ImGui.Button("开始录制"))
        {
            UIImGui.requestRecord = true;
        }
        ImGui.TreePop();
    }
}
