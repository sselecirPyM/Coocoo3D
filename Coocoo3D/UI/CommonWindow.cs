using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using ImGuiNET;
using System;
using System.Numerics;

namespace Coocoo3D.UI;

public class CommonWindow : IWindow
{
    public bool Removing { get; private set; }

    public RecordSystem recordSystem;

    public GameDriverContext gameDriverContext;

    public GameDriver gameDriver;

    public MainCaches mainCaches;

    public UIImGui uiImGui;

    public EditorContext editorContext;

    public void OnGui()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("常用"))
        {
            Common();
        }
        ImGui.End();
    }

    void Common()
    {
        if (ImGui.TreeNode("变换"))
        {
            Vector3 position = UIImGui.position;
            if (ImGui.DragFloat3("位置", ref position, 0.01f))
            {
                UIImGui.position = position;
                UIImGui.positionChange = true;
            }
            Vector3 rotation = UIImGui.rotation / MathF.PI * 180;
            if (ImGui.DragFloat3("旋转", ref rotation))
            {
                UIImGui.rotation = rotation * MathF.PI / 180;
                UIImGui.rotationChange = true;
            }
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("相机"))
        {
            var camera = editorContext.currentChannel.camera;
            ImGui.DragFloat("距离", ref camera.Distance, 0.01f);
            ImGui.DragFloat3("焦点", ref camera.LookAtPoint, 0.05f);
            Vector3 a = camera.Angle / MathF.PI * 180;
            if (ImGui.DragFloat3("角度", ref a))
                camera.Angle = a * MathF.PI / 180;
            float fov = camera.Fov / MathF.PI * 180;
            if (ImGui.DragFloat("FOV", ref fov, 0.5f, 0.1f, 179.9f))
                camera.Fov = fov * MathF.PI / 180;
            ImGui.DragFloat("近裁剪", ref camera.nearClip, 0.01f, 0.01f, float.MaxValue);
            ImGui.DragFloat("远裁剪", ref camera.farClip, 1.0f, 0.01f, float.MaxValue);

            ImGui.Checkbox("使用镜头运动文件", ref camera.CameraMotionOn);
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("录制"))
        {
            if (recordSystem.ffmpegInstalled)
            {
                ImGui.Text("已安装FFmpeg。输出文件名output.mp4");
                ImGui.Checkbox("使用FFmpeg", ref recordSystem.useFFmpeg);
            }
            var recordSettings = recordSystem.recordSettings;
            UIImGui.ShowObject(recordSettings);

            if (ImGui.Button("开始录制"))
            {
                UIImGui.requestRecord = true;
            }
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("帮助"))
        {
            Help();
            ImGui.TreePop();
        }
        if (ImGui.Button("播放"))
        {
            Play();
        }
        ImGui.SameLine();
        if (ImGui.Button("暂停"))
        {
            Pause();
        }
        ImGui.SameLine();
        if (ImGui.Button("停止"))
        {
            Stop();
        }
        if (ImGui.Button("跳到最前"))
        {
            Front();
        }
        ImGui.SameLine();
        if (ImGui.Button("重置物理"))
        {
            gameDriverContext.RefreshScene = true;
        }
        if (ImGui.Button("快进"))
        {
            FastForward();
        }
        ImGui.SameLine();
        if (ImGui.Button("向前5秒"))
        {
            gameDriver.ToPlayMode();
            gameDriverContext.PlayTime -= 5;
            gameDriverContext.RequireRender(true);
        }
        foreach (var input in mainCaches.textureDecodeHandler.inputs)
        {
            ImGui.Text(((TextureLoadTask)input).KnownFile.fullPath);
        }
    }


    void Help()
    {
        if (ImGui.Button("显示帮助"))
            uiImGui.OpenWindow(typeof(HelpWindow));
        if (ImGui.Button("显示Render Buffers"))
            uiImGui.OpenWindow(typeof(RenderBufferWindow));
        ImGui.Checkbox("显示ImGuiDemoWindow", ref UIImGui.demoWindowOpen);
    }

    public void Play()
    {
        gameDriverContext.Playing = true;
        gameDriverContext.PlaySpeed = 1.0f;
        gameDriverContext.RequireRender(false);
    }
    public void Pause()
    {
        gameDriverContext.Playing = false;
    }
    public void Stop()
    {
        gameDriver.ToPlayMode();
        gameDriverContext.Playing = false;
        gameDriverContext.PlayTime = 0;
        gameDriverContext.RequireRender(true);
    }
    public void Rewind()
    {
        gameDriver.ToPlayMode();
        gameDriverContext.Playing = true;
        gameDriverContext.PlaySpeed = -2.0f;
    }
    public void FastForward()
    {
        gameDriver.ToPlayMode();
        gameDriverContext.Playing = true;
        gameDriverContext.PlaySpeed = 2.0f;
    }
    public void Front()
    {
        gameDriver.ToPlayMode();
        gameDriverContext.PlayTime = 0;
        gameDriverContext.RequireRender(true);
    }
    public void Rear()
    {
        gameDriver.ToPlayMode();
        gameDriverContext.PlayTime = 9999;
        gameDriverContext.RequireRender(true);
    }
}
