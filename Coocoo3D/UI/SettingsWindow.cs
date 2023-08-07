using Caprice.Display;
using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using ImGuiNET;
using System;
using System.Numerics;
using System.Reflection;

namespace Coocoo3D.UI;

public class SettingsWindow : IWindow
{
    public Config config;

    public GameDriverContext gameDriverContext;

    public RenderSystem renderSystem;

    public UIImGui uiImGui;

    public MainCaches mainCaches;

    public EditorContext editorContext;

    public WindowSystem windowSystem;

    public bool Removing { get; set; }

    public void OnGui()
    {
        ImGui.SetNextWindowPos(new Vector2(800, 0), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("设置"))
        {
            Settings();
        }
        ImGui.End();
    }


    void Settings()
    {
        ImGui.Checkbox("垂直同步", ref config.VSync);
        ImGui.Checkbox("节省CPU", ref config.SaveCpuPower);
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
        if (ImGui.Button("加载渲染管线"))
        {
            UIImGui.requestSelectRenderPipelines = true;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("默认的渲染管线位于软件的Effects文件夹，软件会在启动时加载这些插件。");
        }

        if (ImGui.Button("添加视口"))
        {
            int c = 1;
            while (true)
            {
                if (!windowSystem.visualChannels.ContainsKey(c.ToString()))
                {
                    uiImGui.OpenWindow(new SceneWindow(windowSystem.AddVisualChannel(c.ToString())));
                    break;
                }
                c++;
            }
        }
        if (ImGui.Button("保存场景"))
        {
            UIImGui.requestSave = true;
        }
        if (ImGui.Button("重新加载纹理"))
        {
            mainCaches.ReloadTextures = true;
        }
        if (ImGui.Button("重新加载Shader"))
        {
            mainCaches.ReloadShaders = true;
        }
    }
}
