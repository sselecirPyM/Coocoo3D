using Coocoo3D.Core;
using ImGuiNET;

namespace Coocoo3D.UI;

public class StatisticsWindow : IWindow
{
    public Statistics statistics;

    public bool Removing { get; private set; }
    bool show = true;
    public void OnGui()
    {
        if (ImGui.Begin("统计", ref show))
        {
            ImGui.Text(string.Format("Fps:{0:f1}", statistics.FramePerSecond));
            ImGui.TextUnformatted("绘制三角形数：" + statistics.DrawTriangleCount);
        }
        ImGui.End();
        if (!show)
        {
            Removing = true;
        }
    }
}
