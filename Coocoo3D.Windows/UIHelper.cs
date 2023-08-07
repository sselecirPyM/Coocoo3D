using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UIImGui = Coocoo3D.UI.UIImGui;

namespace Coocoo3D.Windows;

public class UIHelper
{
    public MainCaches caches;

    public GameDriver gameDriver;

    public Scene scene;

    public EditorContext editorContext;

    public Entity selectedObject;

    public void Initialize()
    {
        editorContext.OnSelectObject += EditorContext_OnSelectObject;
    }

    private void EditorContext_OnSelectObject(Entity obj)
    {
        selectedObject = obj;
    }

    public void OnFrame()
    {
        if (UIImGui.requireOpenFolder)
        {
            UIImGui.requireOpenFolder = false;
            string path = OpenResourceFolder();
            if (!string.IsNullOrEmpty(path))
            {
                DirectoryInfo folder = new DirectoryInfo(path);
                UIImGui.viewRequest = folder;
            }
            gameDriver.RequireRender(false);
        }
        if (UIImGui.requestSelectRenderPipelines)
        {
            UIImGui.requestSelectRenderPipelines = false;
            string path = OpenResourceFolder();
            if (!string.IsNullOrEmpty(path))
            {
                DirectoryInfo folder = new DirectoryInfo(path);
                UIImGui.loadRPRequest = folder;
            }
            gameDriver.RequireRender(false);
        }
        if (UIImGui.viewRequest != null)
        {
            var view = UIImGui.viewRequest;
            UIImGui.viewRequest = null;
            UIImGui.currentFolder = view;
            SetViewFolder(view.GetFileSystemInfos());
            gameDriver.RequireRender(false);
        }
        if (UIImGui.openRequest != null)
        {
            var file = UIImGui.openRequest;
            UIImGui.openRequest = null;

            string ext = file.Extension.ToLower();
            switch (ext)
            {
                case ".pmx":
                case ".gltf":
                    caches.modelLoadHandler.Add(new ModelLoadTask() { path = file.FullName, scene = scene });
                    break;
                case ".vmd":
                    OpenVMDFile(file);
                    break;
                case ".coocoo3dscene":
                    caches.sceneLoadHandler.Add(new SceneLoadTask { path = file.FullName, Scene = scene });
                    break;
            }

            gameDriver.RequireRender(true);
        }
        if (UIImGui.requestRecord)
        {
            UIImGui.requestRecord = false;
            gameDriver.gameDriverContext.NeedRender = 0;
            string path = OpenResourceFolder();
            if (!string.IsNullOrEmpty(path))
            {
                DirectoryInfo folder = new DirectoryInfo(path);
                if (!folder.Exists) return;
                gameDriver.ToRecordMode(folder.FullName);
            }
        }
        if (UIImGui.requestSave)
        {
            UIImGui.requestSave = false;
            FileOpenDialog fileDialog = new FileOpenDialog()
            {
                file = new string(new char[512]),
                fileTitle = new string(new char[512]),
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                filter = ".coocoo3DScene\0*.coocoo3DScene\0\0",
                defExt = "coocoo3DScene",
                flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008,
                structSize = Marshal.SizeOf(typeof(FileOpenDialog))
            };
            fileDialog.maxFile = fileDialog.file.Length;
            fileDialog.maxFileTitle = fileDialog.fileTitle.Length;
            if (GetSaveFileName(fileDialog))
            {
                caches.sceneSaveHandler.Add(new SceneSaveTask() { path = fileDialog.file, Scene = this.scene });
            }
        }
    }

    void OpenVMDFile(FileInfo file)
    {
        using var stream = file.OpenRead();
        using BinaryReader reader = new BinaryReader(stream);
        VMDFormat motionSet = VMDFormat.Load(reader);
        if (motionSet.CameraKeyFrames.Count != 0)
        {
            var camera = editorContext.currentChannel.camera;
            var cameraKeyFrames = new List<CameraKeyFrame>();
            cameraKeyFrames.AddRange(motionSet.CameraKeyFrames);
            camera.cameraMotion.cameraKeyFrames = cameraKeyFrames;
            for (int i = 0; i < cameraKeyFrames.Count; i++)
            {
                CameraKeyFrame frame = cameraKeyFrames[i];
                frame.distance *= 0.1f;
                frame.position *= 0.1f;
                cameraKeyFrames[i] = frame;
            }
            camera.CameraMotionOn = true;
        }
        else
        {
            if (selectedObject.IsAlive && TryGetComponent(selectedObject, out Components.AnimationStateComponent animationState))
            {
                animationState.motionPath = file.FullName;
            }
        }
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

    public static string OpenResourceFile(string filter)
    {
        FileOpenDialog dialog = new FileOpenDialog();
        dialog.structSize = Marshal.SizeOf(typeof(FileOpenDialog));
        dialog.filter = filter;
        dialog.file = new string(new char[2000]);
        dialog.maxFile = dialog.file.Length;

        dialog.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
        dialog.flags = 0x00000008;
        GetOpenFileName(dialog);
        var chars = dialog.file.ToCharArray();

        return new string(chars, 0, Array.IndexOf(chars, '\0'));
    }

    public static string OpenResourceFolder()
    {
        OpenDialogDir openDialogDir = new OpenDialogDir();
        openDialogDir.pszDisplayName = new string(new char[2000]);
        openDialogDir.lpszTitle = "Open Project";
        IntPtr pidlPtr = SHBrowseForFolder(openDialogDir);
        char[] charArray = new char[2000];
        Array.Fill(charArray, '\0');

        SHGetPathFromIDList(pidlPtr, charArray);
        int length = Array.IndexOf(charArray, '\0');
        string fullDirPath = new String(charArray, 0, length);

        return fullDirPath;
    }

    static void SetViewFolder(IReadOnlyList<FileSystemInfo> items)
    {
        lock (UIImGui.storageItems)
        {
            UIImGui.storageItems.Clear();
            foreach (var item in items)
            {
                UIImGui.storageItems.Add(item);
            }
        }
    }

    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    internal static extern bool GetOpenFileName([In, Out] FileOpenDialog ofn);

    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    internal static extern bool GetSaveFileName([In, Out] FileOpenDialog ofn);

    [DllImport("shell32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr SHBrowseForFolder([In, Out] OpenDialogDir ofn);

    [DllImport("shell32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    internal static extern bool SHGetPathFromIDList([In] IntPtr pidl, [In, Out] char[] fileName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal class FileOpenDialog
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public String filter = null;
        public String customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public String file = null;
        public int maxFile = 0;
        public String fileTitle = null;
        public int maxFileTitle = 0;
        public String initialDir = null;
        public String title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public String defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public String templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal class OpenDialogDir
    {
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr pidlRoot = IntPtr.Zero;
        public String pszDisplayName = null;
        public String lpszTitle = null;
        public UInt32 ulFlags = 0;
        public IntPtr lpfn = IntPtr.Zero;
        public IntPtr lParam = IntPtr.Zero;
        public int iImage = 0;
    }
}
