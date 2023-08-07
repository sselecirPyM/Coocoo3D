using ImGuiNET;

namespace Coocoo3D.UI;

public class HelpWindow : IWindow
{
    public bool Removing { get; private set; }

    bool show = true;
    public void OnGui()
    {
        if (ImGui.Begin("帮助", ref show))
        {
            Help();
        }
        ImGui.End();
        if (!show)
        {
            Removing = true;
        }
    }

    void Help()
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
            ImGui.Text(@"当前版本支持pmx、glTF格式模型，
vmd格式动作。支持几乎所有的图片格式。");
            ImGui.TreePop();
        }
    }
}
