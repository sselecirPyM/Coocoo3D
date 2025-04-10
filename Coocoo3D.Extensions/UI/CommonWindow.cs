using Arch.Core;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using ImGuiNET;
using System;
using System.ComponentModel.Composition;
using System.Numerics;

namespace Coocoo3D.Extensions.UI;

[Export(typeof(IWindow))]
[Export(typeof(IEditorAccess))]
[ExportMetadata("MenuItem", "常用")]
public class CommonWindow : IWindow, IEditorAccess
{
    public GameDriverContext gameDriverContext;

    public GameDriver gameDriver;

    public MainCaches mainCaches;

    public UIImGui uiImGui;

    public EditorContext editorContext;

    public World world;
    public string Title { get => "常用"; }

    public void OnGUI()
    {
        Common();
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
            //gameDriverContext.PlayTime -= 5;
            MoveTime(-5);
            gameDriverContext.RequireRender(true);
        }
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
        //gameDriverContext.PlayTime = 0;
        ResetTime(0);
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
        //gameDriverContext.PlayTime = 0;
        ResetTime(0);
        gameDriverContext.RequireRender(true);
    }
    public void Rear()
    {
        gameDriver.ToPlayMode();
        //gameDriverContext.PlayTime = 9999;
        ResetTime(9999);
        gameDriverContext.RequireRender(true);
    }
    QueryDescription q = new QueryDescription().WithAll<AnimationStateComponent>();
    void ResetTime(float time)
    {
        world.Query(q, (ref AnimationStateComponent ani) =>
        {
            ani.Time = time;
        });
    }
    void MoveTime(float time)
    {
        world.Query(q, (ref AnimationStateComponent ani) =>
        {
            ani.Time += time;
        });
    }
}
