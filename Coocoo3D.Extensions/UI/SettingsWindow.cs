using Caprice.Display;
using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System;
using System.ComponentModel.Composition;
using System.Reflection;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[ExportMetadata("MenuItem", "设置")]
public class SettingsWindow : IWindow
{
    public GameDriverContext gameDriverContext;

    public RenderSystem renderSystem;

    public UIImGui uiImGui;

    public EditorContext editorContext;

    public string Title { get => "设置"; }

    public void OnGUI()
    {
        Settings();
    }


    void Settings()
    {
        ImGui.Checkbox("垂直同步", ref gameDriverContext.VSync);
        ImGui.Checkbox("节省CPU", ref gameDriverContext.SaveCpuPower);
        float a = (float)(1.0 / Math.Clamp(gameDriverContext.FrameInterval, 1e-4, 1));
        if (ImGui.DragFloat("帧率限制", ref a, 10, 1, 5000))
        {
            gameDriverContext.FrameInterval = Math.Clamp(1 / a, 1e-4f, 1f);
        }

        if (UIImGui.loadRPRequest != null)
        {
            renderSystem.LoadRenderPipelines(UIImGui.loadRPRequest);
            UIImGui.loadRPRequest = null;
        }
        var currentChannel = editorContext.currentChannel;
        if (currentChannel != null && !currentChannel.disposed)
        {
            var renderPipelineView = currentChannel.renderPipelineView;
            if (renderPipelineView != null)
                uiImGui.ShowParams(renderPipelineView, renderPipelineView.renderPipeline);

            int renderPipelineIndex = 0;

            var rps = renderSystem.RenderPipelineTypes;

            string[] newRPs = new string[rps.Count];
            for (int i = 0; i < rps.Count; i++)
            {
                var textAttribute = rps[i].GetCustomAttribute<TextAttribute>(true);
                if (textAttribute != null)
                    newRPs[i] = textAttribute.Text;
                else
                    newRPs[i] = rps[i].ToString();
                if (rps[i] == renderPipelineView?.renderPipeline?.GetType())
                {
                    renderPipelineIndex = i;
                }
            }

            if (ImGui.Combo("渲染管线", ref renderPipelineIndex, newRPs, rps.Count))
            {
                currentChannel.DelaySetRenderPipeline(rps[renderPipelineIndex]);
            }
        }
    }
}
