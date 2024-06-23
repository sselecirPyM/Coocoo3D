using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using DefaultEcs;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

namespace Coocoo3D.UI;

public class SceneWindow : IWindow2
{
    public bool Removing { get; private set; }

    public EditorContext editorContext;

    public Scene CurrentScene;

    public UIRenderSystem uiRenderSystem;

    public RenderSystem renderSystem;

    public PlatformIO platformIO;

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
        editorContext.OnMounseMoveDelta += EditorContext_OnMounseMoveDelta;
    }

    private void EditorContext_OnMounseMoveDelta(Vector2 obj)
    {
        mouseMoveDelta = obj;
    }

    Vector2 mouseMoveDelta;

    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    Entity selectedObject;
    VisualChannel channel;

    public SceneWindow(VisualChannel visualChannel)
    {
        channel = visualChannel;
    }

    public void OnGUI()
    {
        bool show = true;
        if (channel.Name == "main")
        {
            if (ImGui.Begin(channel.Name))
            {
                SceneView(ImGui.GetIO().MouseWheel, mouseMoveDelta);
            }
            ImGui.End();
        }
        else
        {
            if (ImGui.Begin(channel.Name, ref show))
            {
                SceneView(ImGui.GetIO().MouseWheel, mouseMoveDelta);
            }
            ImGui.End();
        }
        mouseMoveDelta = default(Vector2);
        if (!show)
        {
            Remove();
        }
    }

    void SceneView(float mouseWheelDelta, Vector2 mouseMoveDelta)
    {
        var io = ImGui.GetIO();
        var tex = channel.GetAOV(Caprice.Attributes.AOVType.Color);
        IntPtr imageId;
        if (tex != null)
        {
            imageId = uiRenderSystem.ShowTexture(tex);
        }
        else
        {
            imageId = uiRenderSystem.ShowTexture(null);
        }

        Vector2 pos = ImGui.GetCursorScreenPos();
        Vector2 spaceSize = Vector2.Max(ImGui.GetWindowSize() - new Vector2(20, 40), new Vector2(100, 100));
        channel.sceneViewSize = ((int)spaceSize.X, (int)spaceSize.Y);
        channel.UpdateSize();

        Vector2 texSize;
        (int x, int y) = channel.outputSize;
        texSize = new(x, y);

        float factor = MathF.Max(MathF.Min(spaceSize.X / texSize.X, spaceSize.Y / texSize.Y), 0.01f);
        Vector2 imageSize = texSize * factor;


        ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddImage(imageId, pos, pos + imageSize);
        DrawGizmo(channel, pos, imageSize);

        var camera = channel.camera;
        if (ImGui.IsItemActive())
        {
            if (io.MouseDown[1])
            {
                if (io.KeyCtrl)
                    camera.Distance += (-mouseMoveDelta.Y / 150);
                else
                    camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, mouseMoveDelta.X, 0) / 200);
            }
            if (io.MouseDown[2])
            {
                if (io.KeyCtrl)
                    camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 40);
                else if (io.KeyShift)
                    camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 4000);
                else
                    camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 400);
            }
            editorContext.currentChannel = channel;
        }
        if (ImGui.IsItemHovered())
        {
            camera.Distance += mouseWheelDelta * 0.6f;
            if (platformIO.dropFile != null)
            {
                UIImGui.openRequest = new FileInfo(platformIO.dropFile);
            }
        }
    }

    void DrawGizmo(VisualChannel channel, Vector2 imagePosition, Vector2 imageSize)
    {
        var io = ImGui.GetIO();
        Vector2 mousePos = ImGui.GetMousePos();
        Entity hoveredObject = default;
        string toolTipMessage = "";
        var scene = CurrentScene;
        var vpMatrix = channel.cameraData.vpMatrix;

        UIViewport viewport = new UIViewport
        {
            leftTop = imagePosition,
            rightBottom = imagePosition + imageSize,
        };

        ImGui.PushClipRect(viewport.leftTop, viewport.rightBottom, true);
        var drawList = ImGui.GetWindowDrawList();
        bool hasDrag = false;

        foreach (var obj in scene.world)
        {
            ref var transform = ref obj.Get<Transform>();
            var objectDescription = obj.Get<ObjectDescription>();
            Vector3 position = transform.position;
            Vector2 basePos = imagePosition + (ImGuiExt.TransformToImage(position, vpMatrix, out bool canView)) * imageSize;
            Vector2 diff = Vector2.Abs(basePos - mousePos);
            if (diff.X < 10 && diff.Y < 10 && canView)
            {
                toolTipMessage += objectDescription.Name + "\n";
                hoveredObject = obj;
                drawList.AddNgon(basePos, 10, 0xffffffff, 4);
            }
            if (selectedObject == obj && canView)
            {
                viewport.mvp = vpMatrix;
                bool drag = ImGuiExt.PositionController(drawList, ref UIImGui.position, io.MouseDown[0], viewport);
                if (drag)
                {
                    UIImGui.positionChange = true;
                    hasDrag = true;
                }
            }
            if (TryGetComponent(obj, out VisualComponent visual) && editorContext.showBoundingBox)
            {
                viewport.mvp = transform.GetMatrix() * vpMatrix;
                ImGuiExt.DrawCube(drawList, viewport);
            }
        }
        ImGui.PopClipRect();

        if (ImGui.IsItemHovered() && io.MouseReleased[0] && ImGui.IsItemFocused() && !hasDrag
            && hoveredObject.IsAlive)
        {
            editorContext.SelectObjectMessage(hoveredObject);
        }
        if (!string.IsNullOrEmpty(toolTipMessage))
        {
            ImGui.BeginTooltip();
            ImGui.Text(toolTipMessage);
            ImGui.EndTooltip();
        }
    }

    void Remove()
    {
        Removing = true;
        renderSystem.RemoveVisualChannel(channel.Name);
    }

    static bool TryGetComponent<T>(Entity obj, out T value)
    {
        if (obj.Has<T>())
        {
            value = obj.Get<T>();
            return true;
        }
        else
        {
            value = default(T);
            return false;
        }
    }
}
