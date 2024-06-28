using Caprice.Display;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using DefaultEcs;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Coocoo3D.UI;

public class UIImGui
{
    public PlatformIO platformIO;

    public UIRenderSystem uiRenderSystem;

    public MainCaches mainCaches;

    public RenderPipelineContext renderPipelineContext;

    public EditorContext editorContext;

    public EngineContext engineContext;

    public RenderSystem renderSystem;

    public void GUI()
    {

        var io = ImGui.GetIO();
        Input();

        Vector2 mouseMoveDelta = new Vector2();
        foreach (var delta in platformIO.mouseMoveDelta)
        {
            mouseMoveDelta += delta;
        }
        editorContext.MouseMoveDeltaMessage(mouseMoveDelta);

        var context = renderPipelineContext;
        io.DisplaySize = new Vector2(platformIO.windowSize.Item1, platformIO.windowSize.Item2);
        io.DeltaTime = (float)context.RealDeltaTime;

        positionChange = false;
        rotationChange = false;

        if (selectedObject.IsAlive)
        {
            ref var transform = ref selectedObject.Get<Transform>();
            position = transform.position;
            scale = transform.scale;
            if (rotationCache != transform.rotation)
            {
                rotation = QuaternionToEularYXZ(transform.rotation);
                rotationCache = transform.rotation;
            }
        }

        ImGui.NewFrame();

        DockSpace();

        foreach (var window in editorContext.Windows2)
        {
            window.OnGUI();
        }
        foreach (var window in editorContext.Windows)
        {
            string title = window.Title;
            title ??= window.GetType().Name;
            bool open = true;
            if (window.SimpleWindow)
            {
                if (editorContext.OpeningWindow.Contains(window))
                {
                    ImGui.SetNextWindowFocus();
                }
                if (ImGui.Begin(title, ref open))
                {
                    window.OnGUI();
                }
                ImGui.End();
                if (!open)
                {
                    editorContext.CloseWindow(window);
                }
            }
            else
            {
                window.OnGUI();
            }
        }
        _OpenWindow();


        ImGui.Render();
        if (selectedObject.IsAlive)
        {
            bool transformChange = rotationChange || positionChange;
            if (rotationChange)
            {
                rotationCache = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
            }
            if (transformChange)
            {
                selectedObject.Set(new Transform(position, rotationCache, scale));
            }
        }
        platformIO.dropFile = null;
    }

    void Input()
    {
        var io = ImGui.GetIO();
        for (int i = 0; i < 256; i++)
        {
            io.KeysDown[i] = platformIO.keydown[i];
        }
        foreach (var c in platformIO.inputChars)
            io.AddInputCharacter(c);

        io.KeyCtrl = platformIO.KeyControl;
        io.KeyShift = platformIO.KeyShift;
        io.KeyAlt = platformIO.KeyAlt;
        io.KeySuper = platformIO.KeySuper;


        io.MouseWheel += Interlocked.Exchange(ref platformIO.mouseWheelV, 0);
        io.MouseWheelH += Interlocked.Exchange(ref platformIO.mouseWheelH, 0);
        #region mouse inputs
        for (int i = 0; i < 5; i++)
            io.MouseDown[i] = platformIO.mouseDown[i];
        io.MousePos = platformIO.mousePosition;
        #endregion

        #region outputs
        platformIO.WantCaptureKeyboard = io.WantCaptureKeyboard;
        platformIO.WantCaptureMouse = io.WantCaptureMouse;
        platformIO.WantSetMousePos = io.WantSetMousePos;
        platformIO.WantTextInput = io.WantTextInput;

        platformIO.setMousePos = io.MousePos;
        platformIO.requestCursor = ImGui.GetMouseCursor();
        #endregion
    }

    public void ShowParams(RenderPipelineView view, object tree1)
    {
        if (view == null)
            return;
        ImGui.Separator();
        string filter = ImGuiExt.ImFilter("查找参数", "搜索参数名称");
        var usages1 = UIUsage.GetUIUsage(tree1.GetType());
        foreach (var param in usages1)
        {
            string name = param.MemberInfo.Name;

            if (param.UIShowType != UIShowType.Global)
                continue;
            if (!Contains(param.Name, filter) && !Contains(name, filter))
                continue;

            var member = param.MemberInfo;
            object obj = member.GetValue<object>(tree1);
            var type = obj.GetType();
            if (type.IsEnum)
            {
                if (ImGuiExt.ComboBox(param.Name, ref obj))
                {
                    member.SetValue(tree1, obj);
                }
            }
            else if (param.treeAttribute != null)
            {
                if (ImGui.TreeNode(param.Name))
                {
                    ShowParams(view, member.GetValue<object>(tree1));

                    ImGui.TreePop();
                }
            }
            else
            {
                ShowParam1(param, tree1, () =>
                {
                    return member.GetValue<object>(tree1);
                },
                (object o1) =>
                {
                    member.SetValue(tree1, o1);
                    view.InvalidDependents(name);
                    view.renderPipeline.OnResourceInvald(name);
                });
            }
        }
    }

    public void ShowParams(object source, IDictionary<string, object> parameters)
    {
        ImGui.Separator();
        string filter = ImGuiExt.ImFilter("查找参数", "搜索参数名称");

        foreach (var param in UIUsage.GetUIUsage(source.GetType()))
        {
            string name = param.MemberInfo.Name;

            if (!Contains(param.Name, filter) && !Contains(name, filter))
                continue;

            var member = param.MemberInfo;
            object objMemberValue = member.GetValue<object>(source);
            var type = member.GetGetterType();
            if (type.IsEnum)
            {
                if (parameters.TryGetValue(name, out var parameter1))
                    objMemberValue = parameter1;
                if (objMemberValue is string s && Enum.TryParse(type, s, out var enumValue))
                {
                    objMemberValue = enumValue;
                }
                if (objMemberValue.GetType() != type)
                {
                    objMemberValue = Activator.CreateInstance(type);
                }
                if (ImGuiExt.ComboBox(param.Name, ref objMemberValue))
                {
                    parameters[name] = objMemberValue;
                }
            }
            else
            {
                ShowParam1(param, source, () =>
                {
                    parameters.TryGetValue(name, out var parameter);
                    return parameter;
                },
                (object o1) => { parameters[name] = o1; },
                true);
            }
        }
    }

    void ShowParam1(UIUsage param, object source, Func<object> getter, Action<object> setter, bool viewOverride = false)
    {
        var member = param.MemberInfo;
        object obj = member.GetValue<object>(source);
        var type = member.GetGetterType();

        string displayName = param.Name;
        string name = member.Name;

        bool propertyOverride = false;
        object parameter = getter.Invoke();
        if (parameter != null && type == parameter.GetType())
        {
            propertyOverride = viewOverride;
            obj = parameter;
        }
        if (propertyOverride)
            ImGui.PushStyleColor(ImGuiCol.Text, 0xffaaffaa);
        var sliderAttribute = param.sliderAttribute;
        var colorAttribute = param.colorAttribute;
        var dragFloatAttribute = param.dragFloatAttribute;
        var dragIntAttribute = param.dragIntAttribute;
        switch (obj)
        {
            case bool val:
                if (ImGui.Checkbox(displayName, ref val))
                {
                    setter.Invoke(val);
                }
                break;
            case float val:
                if (sliderAttribute != null)
                {
                    if (ImGui.SliderFloat(displayName, ref val, sliderAttribute.Min, sliderAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector2 val:
                if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat2(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector3 val:
                if (colorAttribute != null)
                {
                    if (ImGui.ColorEdit3(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat3(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector4 val:
                if (colorAttribute != null)
                {
                    if (ImGui.ColorEdit4(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat4(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case int val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt(displayName, ref val, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt2(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt3(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int, int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt4(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case string val:
                if (ImGui.InputText(displayName, ref val, 256))
                {
                    setter.Invoke(val);
                }
                break;
            case Coocoo3DGraphics.Texture2D tex2d:

                if (ShowTexture(displayName, "global", name, tex2d, out var newTexture))
                {
                    setter.Invoke(newTexture);
                }
                break;
            default:
                ImGui.Text(displayName + ": 不支持的类型");
                break;
        }
        if (propertyOverride)
            ImGui.PopStyleColor();

        if (param.Description != null && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(param.Description);
            ImGui.EndTooltip();
        }
    }

    public bool ShowTexture(string displayName, string id, string slot,
        Coocoo3DGraphics.Texture2D texture, out Coocoo3DGraphics.Texture2D newTexture)
    {
        bool textureChange = false;
        var cache = mainCaches;

        newTexture = texture;

        IntPtr imageId = uiRenderSystem.ShowTexture(texture);
        ImGui.Text(displayName);
        Vector2 imageSize = new Vector2(120, 120);
        if (ImGui.ImageButton(imageId, imageSize))
        {
            StartSelectResource(id, slot);
        }
        if (CheckResourceSelect(id, slot, out string result))
        {
            newTexture = cache.GetTexturePreloaded(result);
            textureChange = true;
        }
        if (platformIO.dropFile != null && ImGui.IsItemHovered())
        {
            newTexture = cache.GetTexturePreloaded(platformIO.dropFile);
            textureChange = true;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Image(imageId, new Vector2(256, 256));
            ImGui.EndTooltip();
        }
        return textureChange;
    }

    public static void ShowObject(object source)
    {
        foreach (var param in UIUsage.GetUIUsage(source.GetType()))
        {
            ShowObjectParam1(param, source);
        }
    }

    static void ShowObjectParam1(UIUsage param, object source)
    {
        var member = param.MemberInfo;
        object obj = member.GetValue<object>(source);
        //var type = member.GetGetterType();

        string displayName = param.Name;
        //string name = member.Name;

        var setter = (object u) =>
        {
            member.SetValue(source, u);
        };

        var sliderAttribute = param.sliderAttribute;
        var colorAttribute = param.colorAttribute;
        var dragFloatAttribute = param.dragFloatAttribute;
        var dragIntAttribute = param.dragIntAttribute;
        switch (obj)
        {
            case bool val:
                if (ImGui.Checkbox(displayName, ref val))
                {
                    setter.Invoke(val);
                }
                break;
            case float val:
                if (sliderAttribute != null)
                {
                    if (ImGui.SliderFloat(displayName, ref val, sliderAttribute.Min, sliderAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector2 val:
                if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat2(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector3 val:
                if (colorAttribute != null)
                {
                    if (ImGui.ColorEdit3(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat3(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case Vector4 val:
                if (colorAttribute != null)
                {
                    if (ImGui.ColorEdit4(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                    {
                        setter.Invoke(val);
                    }
                }
                else if (dragFloatAttribute != null)
                {
                    if (ImGui.DragFloat4(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case int val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt(displayName, ref val, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt2(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt3(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case ValueTuple<int, int, int, int> val:
                if (dragIntAttribute != null)
                {
                    if (ImGui.DragInt4(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                    {
                        setter.Invoke(val);
                    }
                }
                break;
            case string val:
                if (ImGui.InputText(displayName, ref val, 256))
                {
                    setter.Invoke(val);
                }
                break;
            default:
                ImGui.Text(displayName + ": 不支持的类型");
                break;
        }

        if (param.Description != null && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(param.Description);
            ImGui.EndTooltip();
        }
    }

    void DockSpace()
    {
        ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewPort.Size - new Vector2(0, 14), ImGuiCond.Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            MainMenu();
            if (editorContext.currentChannel != null)
            {
                var tex = editorContext.currentChannel.GetAOV(Caprice.Attributes.AOVType.Color);
                IntPtr imageId = uiRenderSystem.ShowTexture(tex);
                ImGui.GetWindowDrawList().AddImage(imageId, viewPort.WorkPos, viewPort.WorkPos + viewPort.WorkSize);
            }
            ImGuiDockNodeFlags dockNodeFlag = ImGuiDockNodeFlags.PassthruCentralNode;
            ImGui.DockSpace(ImGui.GetID("MyDockSpace"), Vector2.Zero, dockNodeFlag);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    void MainMenu()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("命令"))
            {
                ImGuiExt.CommandMenu("UICommand");
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("窗口"))
            {
                foreach (var factory in extensionFactory.Windows)
                {
                    if (ImGui.MenuItem(factory.Metadata.MenuItem))
                    {
                        editorContext.OpenWindow(factory.Value);
                    }
                }
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }

    static void StartSelectResource(string id, string slot)
    {
        requestOpenResource = true;
        fileOpenId = id;
        fileOpenSlot = slot;
    }

    static bool CheckResourceSelect(string id, string slot, out string selectedResource)
    {
        if (id == fileOpenId && slot == fileOpenSlot && fileOpenResult != null)
        {
            selectedResource = fileOpenResult;
            fileOpenResult = null;
            fileOpenId = null;
            fileOpenSlot = null;
            return true;
        }
        else
            selectedResource = null;
        return false;
    }

    public static void StartEditParam()
    {
        paramEdit = new RenderMaterial() { Type = UIShowType.Material };
        requestParamEdit = true;
    }

    static string fileOpenId = null;

    public static Vector3 position;
    public static Vector3 rotation;
    public static Vector3 scale;
    public static Quaternion rotationCache;
    public static bool rotationChange;
    public static bool positionChange;

    public static bool requestRecord;

    public static bool requestSelectRenderPipelines;

    public static List<FileSystemInfo> storageItems = new List<FileSystemInfo>();
    public static DirectoryInfo currentFolder;
    public static DirectoryInfo viewRequest;
    public static DirectoryInfo loadRPRequest;
    //public static List<bool> gameObjectSelected = new List<bool>();

    static Vector3 QuaternionToEularYXZ(Quaternion quaternion)
    {
        double ii = (double)quaternion.X * quaternion.X;
        double jj = (double)quaternion.Y * quaternion.Y;
        double kk = (double)quaternion.Z * quaternion.Z;
        double ei = (double)quaternion.W * quaternion.X;
        double ej = (double)quaternion.W * quaternion.Y;
        double ek = (double)quaternion.W * quaternion.Z;
        double ij = (double)quaternion.X * quaternion.Y;
        double ik = (double)quaternion.X * quaternion.Z;
        double jk = (double)quaternion.Y * quaternion.Z;
        Vector3 result = new Vector3();
        result.X = (float)Math.Asin(2.0 * (ei - jk));
        result.Y = (float)Math.Atan2(2.0 * (ej + ik), 1 - 2.0 * (ii + jj));
        result.Z = (float)Math.Atan2(2.0 * (ek + ij), 1 - 2.0 * (ii + kk));
        return result;
    }

    public void Initialize()
    {
        var caches = mainCaches;
        ImGui.SetCurrentContext(ImGui.CreateContext());
        Coocoo3DGraphics.Uploader uploader = new Coocoo3DGraphics.Uploader();
        var io = ImGui.GetIO();
        io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());
        unsafe
        {
            byte* data;
            io.Fonts.GetTexDataAsRGBA32(out data, out int width, out int height, out int bytesPerPixel);
            int size = width * height * bytesPerPixel;
            Span<byte> spanByte1 = new Span<byte>(data, size);

            uploader.Texture2DRawLessCopy(spanByte1.ToArray(), Vortice.DXGI.Format.R8G8B8A8_UNorm, width, height, 1);
        }

        var texture2D = uiRenderSystem.uiTexture = new Coocoo3DGraphics.Texture2D();
        io.Fonts.TexID = new IntPtr(UIRenderSystem.uiTextureIndex);
        caches.ProxyCall(() =>
        {
            mainCaches.graphicsContext1.UploadTexture(texture2D, uploader);
        });
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
        InitKeyMap();
        OpenWindow(typeof(CommonWindow));
        OpenWindow(typeof(SceneHierachyWindow));
        OpenWindow(typeof(GameObjectWindow));
        OpenWindow(typeof(PopupsWindow));
        OpenWindow(typeof(ResourceWindow));

        editorContext.currentChannel = renderSystem.AddVisualChannel("main");
        OpenWindow(new SceneWindow(editorContext.currentChannel));

        _OpenWindow();
    }

    ExtensionFactory extensionFactory { get => engineContext.extensionFactory; }

    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    static void InitKeyMap()
    {
        var io = ImGui.GetIO();

        io.KeyMap[(int)ImGuiKey.Tab] = (int)ImGuiKey.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)ImGuiKey.LeftArrow;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)ImGuiKey.RightArrow;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)ImGuiKey.UpArrow;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)ImGuiKey.DownArrow;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)ImGuiKey.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)ImGuiKey.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)ImGuiKey.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)ImGuiKey.End;
        io.KeyMap[(int)ImGuiKey.Insert] = (int)ImGuiKey.Insert;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)ImGuiKey.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)ImGuiKey.Backspace;
        io.KeyMap[(int)ImGuiKey.Space] = (int)ImGuiKey.Space;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)ImGuiKey.Enter;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)ImGuiKey.Escape;
        io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)ImGuiKey.KeyPadEnter;
        io.KeyMap[(int)ImGuiKey.A] = 'A';
        io.KeyMap[(int)ImGuiKey.C] = 'C';
        io.KeyMap[(int)ImGuiKey.V] = 'V';
        io.KeyMap[(int)ImGuiKey.X] = 'X';
        io.KeyMap[(int)ImGuiKey.Y] = 'Y';
        io.KeyMap[(int)ImGuiKey.Z] = 'Z';

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    }

    public static bool requestOpenResource = false;
    public static string fileOpenResult;
    public static string fileOpenSlot;

    public static bool requestParamEdit = false;
    public static RenderMaterial paramEdit;
    Entity selectedObject;

    static bool Contains(string input, string filter)
    {
        return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    List<IWindow2> wantAdded = new List<IWindow2>();
    List<Type> wantToOpen = new List<Type>();
    public static ConcurrentQueue<PlatformIOTask> UITaskQueue = new ConcurrentQueue<PlatformIOTask>();

    public void OpenWindow(Type type)
    {
        wantToOpen.Add(type);
    }
    public void OpenWindow(IWindow2 window)
    {
        wantAdded.Add(window);
    }
    void _OpenWindow()
    {
        foreach (var type in wantToOpen)
        {
            if (type.IsAssignableTo(typeof(IWindow2)) && !editorContext.Windows2.Any(u => u.GetType() == type))
            {
                IWindow2 o = (IWindow2)Activator.CreateInstance(type);
                engineContext.FillProperties(o);
                engineContext.InitializeObject(o);
                editorContext.Windows2.Add(o);
            }
        }

        foreach (var window in wantAdded)
        {
            engineContext.FillProperties(window);
            engineContext.InitializeObject(window);
            editorContext.Windows2.Add(window);
        }
        wantToOpen.Clear();
        wantAdded.Clear();

        foreach (var window in editorContext.Windows2)
        {
            if (window.Removing)
                editorContext.Windows2.Remove(window);
        }

        foreach (var window in editorContext.OpeningWindow)
        {
            engineContext.FillProperties(window);
            if (editorContext.Windows.Add(window))
            {
                window.OnShow();
            }
        }
        editorContext.OpeningWindow.Clear();
        foreach (var window in editorContext.RemovingWindow)
        {
            if (editorContext.Windows.Remove(window))
            {
                window.OnClose();
            }
        }
        editorContext.RemovingWindow.Clear();
    }
}
