using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Newtonsoft.Json;

namespace Coocoo3D.UI
{
    public static class UIHelper
    {
        public static void OnFrame(Coocoo3DMain main)
        {
            if (UIImGui.requireOpenFolder.SetFalse())
            {
                string path = OpenResourceFolder();
                if (!string.IsNullOrEmpty(path))
                {
                    DirectoryInfo folder = new DirectoryInfo(path);
                    UIImGui.viewRequest = folder;
                    main.mainCaches.AddFolder(folder);
                }
                main.RequireRender();
            }
            if (UIImGui.requestSelectRenderPipelines.SetFalse())
            {
                string path = OpenResourceFolder();
                if (!string.IsNullOrEmpty(path))
                {
                    DirectoryInfo folder = new DirectoryInfo(path);
                    UIImGui.renderPipelinesRequest = folder;
                    main.mainCaches.AddFolder(folder);
                }
                main.RequireRender();
            }
            if (UIImGui.viewRequest != null)
            {
                var view = UIImGui.viewRequest;
                UIImGui.viewRequest = null;
                UIImGui.currentFolder = view;
                SetViewFolder(view.GetFileSystemInfos());
                main.RequireRender();
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
                        LoadEntityIntoScene(main, file);
                        break;
                    case ".vmd":
                        BinaryReader reader = new BinaryReader(file.OpenRead());
                        VMDFormat motionSet = VMDFormat.Load(reader);
                        if (motionSet.CameraKeyFrames.Count != 0)
                        {
                            var camera = main.windowSystem.currentChannel.camera;
                            camera.cameraMotion.cameraKeyFrames = motionSet.CameraKeyFrames;
                            for (int i = 0; i < camera.cameraMotion.cameraKeyFrames.Count; i++)
                            {
                                CameraKeyFrame frame = camera.cameraMotion.cameraKeyFrames[i];
                                frame.distance *= 0.1f;
                                frame.position *= 0.1f;
                                camera.cameraMotion.cameraKeyFrames[i] = frame;
                            }
                            camera.CameraMotionOn = true;
                        }
                        else
                        {
                            foreach (var gameObject in main.SelectedGameObjects)
                            {
                                var renderer = gameObject.GetComponent<Components.MMDRendererComponent>();
                                if (renderer != null) { renderer.motionPath = file.FullName; }
                            }

                            main.GameDriverContext.RequireResetPhysics = true;
                        }
                        break;
                    case ".coocoo3dscene":
                        var scene = ReadJsonStream<Coocoo3DScene>(file.OpenRead());
                        scene.ToScene(main);
                        break;
                }
                main.RequireRender(true);
            }
            if (UIImGui.requestRecord.SetFalse())
            {
                main.GameDriverContext.NeedRender = 0;
                string path = OpenResourceFolder();
                if (!string.IsNullOrEmpty(path))
                {
                    DirectoryInfo folder = new DirectoryInfo(path);
                    if (!folder.Exists) return;
                    main.ToRecordMode(folder.FullName);
                }
            }
            if (UIImGui.requestSave.SetFalse())
            {
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
                    var scene = Coocoo3DScene.FromScene(main);

                    SaveJsonStream(new FileInfo(fileDialog.file).Create(), scene);
                }
            }
        }

        static T ReadJsonStream<T>(Stream stream)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamReader reader1 = new StreamReader(stream);
            return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
        }

        static void SaveJsonStream<T>(Stream stream, T val)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamWriter writer = new StreamWriter(stream);
            jsonSerializer.Serialize(writer, val);
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

        public static void LoadEntityIntoScene(Coocoo3DMain main, FileInfo pmxFile)
        {
            string path = pmxFile.FullName;
            ModelPack modelPack = main.mainCaches.GetModel(path);
            PreloadTextures(main, modelPack);
            if (modelPack.pmx != null)
            {
                GameObject gameObject = new GameObject();
                gameObject.LoadPmx(modelPack);
                main.AddGameObject(gameObject);
            }
            else
            {
                GameObject gameObject = new GameObject();
                gameObject.Name = Path.GetFileNameWithoutExtension(path);
                modelPack.LoadMeshComponent(gameObject);
                main.AddGameObject(gameObject);
            }

            main.RequireRender();
        }

        public static void PreloadTextures(Coocoo3DMain main, ModelPack model)
        {
            foreach (var tex in model.textures)
                main.mainCaches.Texture(tex);
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
}
