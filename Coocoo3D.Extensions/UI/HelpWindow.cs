using Coocoo3D.UI;
using ImGuiNET;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[ExportMetadata("MenuItem", "帮助")]
public class HelpWindow : IWindow
{
    public string Title { get => "帮助"; }

    public void OnGUI()
    {
        if (ImGui.TreeNode("基本操作"))
        {
            ImGui.Text(@"旋转视角 - 按住鼠标右键拖动
平移镜头 - 按住鼠标中键拖动
拉近、拉远镜头 - 鼠标滚轮
修改物体位置、旋转 - 双击修改，或者在数字上按住左键然后拖动
打开文件 - 将文件拖入窗口，或者在资源窗口打开文件夹。");
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("支持格式"))
        {
            ImGui.Text(@"支持模型: pmx, glTF
支持动画: vmd");
            ImGui.TreePop();
        }
    }
}
