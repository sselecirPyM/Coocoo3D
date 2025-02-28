using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[ExportMetadata("MenuItem", "统计")]
public class StatisticsWindow : IWindow
{
    public Statistics statistics;

    public string Title { get => "统计"; }

    public void OnGUI()
    {
        ImGui.Text(string.Format("Fps:{0:f1}", statistics.FramePerSecond));
        ImGui.TextUnformatted("绘制三角形数：" + statistics.DrawTriangleCount);
    }
}
