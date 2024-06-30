using Coocoo3D.Core;
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

    public SceneExtensionsSystem sceneExtensions;

    public nint hwnd;

    public bool wantQuit;

    public void Initialize()
    {
    }

    public void OnFrame()
    {
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

        while (UIImGui.UITaskQueue.TryDequeue(out var task))
        {
            switch (task.type)
            {
                case UI.PlatformIOTaskType.OpenFile:
                case UI.PlatformIOTaskType.SaveFile:
                    SaveFile(task.title, task.filter, task.fileExtension, task.callback);
                    break;
                case UI.PlatformIOTaskType.SaveFolder:
                    SaveFolder(task.title, task.callback);
                    break;
                case UI.PlatformIOTaskType.Exit:
                    wantQuit = true;
                    break;
            }
        }
    }

    public void SaveFile(string title, string filter, string defaultExt, Action<string> callback)
    {
        FileOpenDialog fileDialog = new FileOpenDialog()
        {
            dlgOwner = hwnd,
            file = new string(new char[512]),
            fileTitle = new string(new char[512]),
            title = title,
            initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
            filter = filter,
            defExt = defaultExt,
            flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008,
            structSize = Marshal.SizeOf(typeof(FileOpenDialog))
        };
        fileDialog.maxFile = fileDialog.file.Length;
        fileDialog.maxFileTitle = fileDialog.fileTitle.Length;
        if (GetSaveFileName(fileDialog))
        {
            callback(fileDialog.file);
        }
    }
    public void SaveFolder(string title, Action<string> callback)
    {
        OpenDialogDir openDialogDir = new OpenDialogDir();
        openDialogDir.pszDisplayName = new string(new char[2000]);
        openDialogDir.lpszTitle = title;
        openDialogDir.hwndOwner = hwnd;
        IntPtr pidlPtr = SHBrowseForFolder(openDialogDir);
        char[] charArray = new char[2000];
        Array.Fill(charArray, '\0');

        if (SHGetPathFromIDList(pidlPtr, charArray))
        {
            int length = Array.IndexOf(charArray, '\0');
            string fullDirPath = new String(charArray, 0, length);
            callback(fullDirPath);
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
