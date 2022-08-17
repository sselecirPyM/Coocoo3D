using Coocoo3D.Components;
using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline
{
    public class MainCaches : IDisposable
    {
        public Dictionary<string, KnownFile> KnownFiles = new();
        public ConcurrentDictionary<string, DirectoryInfo> KnownFolders = new();

        public VersionedDictionary<string, Texture2DPack> TextureCaches = new();
        public Dictionary<string, Texture2DPack> TextureOnDemand = new();
        public Dictionary<string, Texture2DPack> TextureLoading = new();

        public VersionedDictionary<string, ModelPack> ModelPackCaches = new();
        public VersionedDictionary<string, MMDMotion> Motions = new();
        public VersionedDictionary<string, ComputeShader> ComputeShaders = new();

        public VersionedDictionary<string, RayTracingShader> RayTracingShaders = new();
        public VersionedDictionary<string, PSO> PipelineStateObjects = new();
        public VersionedDictionary<string, RTPSO> RTPSOs = new();
        public VersionedDictionary<string, TextureCube> TextureCubes = new();
        public VersionedDictionary<string, Assembly> Assemblies = new();
        public VersionedDictionary<string, RootSignature> RootSignatures = new();

        public ConcurrentQueue<(Texture2D, Uploader)> TextureReadyToUpload = new();
        public ConcurrentQueue<Mesh> MeshReadyToUpload = new();

        public MainCaches()
        {
            KnownFolders.TryAdd(Environment.CurrentDirectory, new DirectoryInfo(Environment.CurrentDirectory));
            KnownFolders.TryAdd("Assets", new DirectoryInfo(Path.GetFullPath("Assets")));
        }

        public Action _RequireRender;

        public bool ReloadTextures = false;
        public bool ReloadShaders = false;

        public void AddFolder(DirectoryInfo folder)
        {
            KnownFolders[folder.FullName] = folder;
        }

        public void PreloadTexture(string fullPath)
        {
            if (!TextureOnDemand.ContainsKey(fullPath))
            {
                AddFolder(new DirectoryInfo(Path.GetDirectoryName(fullPath)));
                TextureOnDemand[fullPath] = new Texture2DPack() { fullPath = fullPath };
            }
        }

        ConcurrentDictionary<Texture2DPack, Uploader> uploaders = new();
        public void OnFrame()
        {
            if (ReloadShaders.SetFalse())
            {
                foreach (var knownFile in KnownFiles)
                    knownFile.Value.requireReload = true;

                Console.Clear();
            }
            if (ReloadTextures.SetFalse() && TextureCaches.Count > 0)
            {
                var packs = TextureCaches.ToList();
                foreach (var pair in packs)
                {
                    if (!TextureOnDemand.ContainsKey(pair.Key))
                        TextureOnDemand.Add(pair.Key, new Texture2DPack() { fullPath = pair.Value.fullPath });
                }
                foreach (var pair in KnownFiles)
                {
                    pair.Value.requireReload = true;
                }
            }

            if (TextureOnDemand.Count == 0 && TextureLoading.Count == 0) return;

            foreach (var notLoad in TextureOnDemand.Where(u => { return u.Value.loadTask == null; }))
            {
                var tex1 = TextureCaches.GetOrCreate(notLoad.Key);
                tex1.Mark(GraphicsObjectStatus.loading);
                if (TextureLoading.Count > 6) continue;

                InitFolder(Path.GetDirectoryName(notLoad.Value.fullPath));
                (Texture2DPack, KnownFile) taskParam = new();
                taskParam.Item1 = notLoad.Value;
                taskParam.Item2 = KnownFiles.GetOrCreate(notLoad.Value.fullPath, (string path) => new KnownFile()
                {
                    fullPath = path,
                });

                notLoad.Value.loadTask = Task.Factory.StartNew((object a) =>
                {
                    var taskParam1 = ((Texture2DPack, KnownFile))a;
                    var texturePack1 = taskParam1.Item1;
                    var knownFile = taskParam1.Item2;

                    var folder = KnownFolders[Path.GetDirectoryName(knownFile.fullPath)];

                    if (LoadTexture(folder, texturePack1, knownFile))
                        texturePack1.Status = GraphicsObjectStatus.loaded;
                    else
                        texturePack1.Status = GraphicsObjectStatus.error;
                    _RequireRender();
                }, taskParam);
                TextureLoading[notLoad.Key] = notLoad.Value;
            }

            foreach (var loadCompleted in TextureLoading.Where(u => { return u.Value.loadTask != null && u.Value.loadTask.IsCompleted; }).ToArray())
            {
                var tex1 = TextureCaches.GetOrCreate(loadCompleted.Key);
                var packLoading = loadCompleted.Value;
                if (packLoading.loadTask.Status == TaskStatus.RanToCompletion &&
                    TextureReadyToUpload.Count < 3 &&
                   (packLoading.Status == GraphicsObjectStatus.loaded ||
                    packLoading.Status == GraphicsObjectStatus.error))
                {
                    tex1.fullPath = packLoading.fullPath;
                    tex1.texture2D.Status = packLoading.Status;
                    tex1.Status = packLoading.Status;

                    if (uploaders.TryRemove(packLoading, out Uploader uploader))
                    {
                        tex1.texture2D.Name = tex1.fullPath;
                        TextureReadyToUpload.Enqueue(new(tex1.texture2D, uploader));
                    }
                    TextureOnDemand.Remove(loadCompleted.Key);
                    TextureLoading.Remove(loadCompleted.Key);
                }
            }
        }

        bool LoadTexture(DirectoryInfo folder, Texture2DPack texturePack, KnownFile knownFile)
        {
            try
            {
                if (!knownFile.IsModified(folder.GetFiles()) && texturePack.initialized) return true;
                Uploader uploader = new Uploader();
                if (texturePack.ReloadTexture(knownFile.file, uploader))
                {
                    uploaders[texturePack] = uploader;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public T GetT<T>(VersionedDictionary<string, T> caches, string path, Func<FileInfo, T> createFun) where T : class
        {
            return GetT(caches, path, path, createFun);
        }
        public T GetT<T>(VersionedDictionary<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
        {
            var knownFile = KnownFiles.GetOrCreate(realPath, () => new KnownFile()
            {
                fullPath = realPath,
            });
            int modifyCount = knownFile.modifiyCount;
            if (knownFile.requireReload.SetFalse() || knownFile.file == null)
            {
                string folderPath = Path.GetDirectoryName(realPath);
                if (!InitFolder(folderPath) && !Path.IsPathRooted(folderPath))
                    return null;
                var folder = (Path.IsPathRooted(folderPath)) ? new DirectoryInfo(folderPath) : KnownFolders[folderPath];
                try
                {
                    modifyCount = knownFile.GetModifyCount(folder.GetFiles());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            if (!caches.TryGetValue(path, out var file) || modifyCount > caches.GetVersion(path))
            {
                try
                {
                    caches.SetVersion(path, modifyCount);
                    var file1 = createFun(knownFile.file);
                    caches[path] = file1;
                    if (file is IDisposable disposable)
                        disposable?.Dispose();
                    file = file1;
                }
                catch (Exception e)
                {
                    if (file is IDisposable disposable)
                        disposable?.Dispose();
                    file = null;
                    caches[path] = file;
                    Console.WriteLine(e.Message);
                }
            }
            return file;
        }

        public Texture2D GetTextureLoaded(string path, GraphicsContext graphicsContext)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return GetT(TextureCaches, path, file =>
            {
                var texturePack1 = new Texture2DPack();
                texturePack1.fullPath = path;
                Uploader uploader = new Uploader();
                texturePack1.ReloadTexture(file, uploader);
                graphicsContext.UploadTexture(texturePack1.texture2D, uploader);
                texturePack1.Mark(GraphicsObjectStatus.loaded);
                return texturePack1;
            }).texture2D;
        }

        public ModelPack GetModel(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (ModelPackCaches)
                return GetT(ModelPackCaches, path, file =>
                {
                    var modelPack = new ModelPack();
                    modelPack.fullPath = path;

                    if (".pmx".Equals(file.Extension, StringComparison.CurrentCultureIgnoreCase))
                    {
                        modelPack.LoadPMX(path);
                    }
                    else
                    {
                        modelPack.LoadModel(path);
                    }
                    MeshReadyToUpload.Enqueue(modelPack.GetMesh());
                    return modelPack;
                });
        }

        public MMDMotion GetMotion(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return GetT(Motions, path, file =>
            {
                BinaryReader reader = new BinaryReader(file.OpenRead());
                VMDFormat motionSet = VMDFormat.Load(reader);

                var motion = new MMDMotion();
                motion.Load(motionSet);
                return motion;
            });
        }

        public Type[] GetTypes(string path, Type baseType)
        {
            var assembly = GetAssembly(path);
            return assembly.GetTypes().Where(u => u.IsSubclassOf(baseType)).ToArray();
        }

        public Assembly GetAssembly(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return GetT(Assemblies, path, file =>
            {
                if (file.Extension.Equals(".cs"))
                {
                    byte[] datas = CompileScripts(path);
                    if (datas != null && datas.Length > 0)
                    {
                        return Assembly.Load(datas);
                    }
                    else
                        return null;
                }
                else
                {
                    return Assembly.LoadFile(path);
                }
            });
        }

        public static byte[] CompileScripts(string path)
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));

                MemoryStream memoryStream = new MemoryStream();
                List<MetadataReference> refs = new List<MetadataReference>() {
                    MetadataReference.CreateFromFile (typeof (object).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (List<int>).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (System.Text.ASCIIEncoding).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (JsonConvert).Assembly.Location),
                    MetadataReference.CreateFromFile (Assembly.GetExecutingAssembly().Location),
                    MetadataReference.CreateFromFile (typeof (SixLabors.ImageSharp.Image).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (GraphicsContext).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (Vortice.Dxc.Dxc).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (SharpGen.Runtime.CppObject).Assembly.Location),
                    MetadataReference.CreateFromFile (typeof (SharpGen.Runtime.ComObject).Assembly.Location),
                };
                refs.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(u => u.GetName().Name.Contains("netstandard") ||
                    u.GetName().Name.Contains("System")).Select(u => MetadataReference.CreateFromFile(u.Location)));
                var compilation = CSharpCompilation.Create(Path.GetFileName(path), new[] { syntaxTree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                var result = compilation.Emit(memoryStream);
                if (!result.Success)
                {
                    foreach (var diag in result.Diagnostics)
                        Console.WriteLine(diag.ToString());
                }
                return memoryStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public ComputeShader GetComputeShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(ComputeShaders, path, file =>
            {
                ComputeShader computeShader = new ComputeShader();
                computeShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path));
                return computeShader;
            });
        }

        public ComputeShader GetComputeShaderWithKeywords(IReadOnlyList<(string, string)> keywords, string path)
        {
            string xPath;
            if (keywords != null)
            {
                //keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                xPath = stringBuilder.ToString();
            }
            else
            {
                xPath = path;
            }
            if (string.IsNullOrEmpty(path)) return null;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            return GetT(ComputeShaders, xPath, path, file =>
            {
                DxcDefine[] dxcDefines = null;
                if (keywords != null)
                {
                    dxcDefines = new DxcDefine[keywords.Count];
                    for (int i = 0; i < keywords.Count; i++)
                    {
                        dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                    }
                }
                ComputeShader computeShader = new ComputeShader();
                computeShader.Initialize(LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path, dxcDefines));
                return computeShader;
            });
        }

        public RayTracingShader GetRayTracingShader(string path)
        {
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
            var rayTracingShader = GetT(RayTracingShaders, path, file =>
            {
                return ReadJsonStream<RayTracingShader>(file.OpenRead());
            });
            return rayTracingShader;
        }

        public RTPSO GetRTPSO(IReadOnlyList<(string, string)> keywords, RayTracingShader shader, string path)
        {
            string xPath;
            if (keywords != null)
            {
                //keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                xPath = stringBuilder.ToString();
            }
            else
            {
                xPath = path;
            }
            return GetT(RTPSOs, xPath, path, file =>
            {
                try
                {
                    string source = File.ReadAllText(file.FullName);
                    DxcDefine[] dxcDefines = null;
                    if (keywords != null)
                    {
                        dxcDefines = new DxcDefine[keywords.Count];
                        for (int i = 0; i < keywords.Count; i++)
                        {
                            dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                        }
                    }
                    byte[] result = LoadShader(DxcShaderStage.Library, source, "", path, dxcDefines);

                    if (shader.hitGroups != null)
                    {
                        foreach (var pair in shader.hitGroups)
                            pair.Value.name = pair.Key;
                    }

                    RTPSO rtpso = new RTPSO();
                    rtpso.datas = result;
                    if (shader.rayGenShaders != null)
                        rtpso.rayGenShaders = shader.rayGenShaders.Values.ToArray();
                    else
                        rtpso.rayGenShaders = new RayTracingShaderDescription[0];
                    if (shader.hitGroups != null)
                        rtpso.hitGroups = shader.hitGroups.Values.ToArray();
                    else
                        rtpso.hitGroups = new RayTracingShaderDescription[0];

                    if (shader.missShaders != null)
                        rtpso.missShaders = shader.missShaders.Values.ToArray();
                    else
                        rtpso.missShaders = new RayTracingShaderDescription[0];

                    rtpso.exports = shader.GetExports();
                    List<ResourceAccessType> ShaderAccessTypes = new();
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    if (shader.CBVs != null)
                        for (int i = 0; i < shader.CBVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.CBV);
                    if (shader.SRVs != null)
                        for (int i = 0; i < shader.SRVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.SRVTable);
                    if (shader.UAVs != null)
                        for (int i = 0; i < shader.UAVs.Count; i++)
                            ShaderAccessTypes.Add(ResourceAccessType.UAVTable);
                    rtpso.shaderAccessTypes = ShaderAccessTypes.ToArray();
                    ShaderAccessTypes.Clear();
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    ShaderAccessTypes.Add(ResourceAccessType.SRV);
                    if (shader.localCBVs != null)
                        foreach (var cbv in shader.localCBVs)
                            ShaderAccessTypes.Add(ResourceAccessType.CBV);
                    if (shader.localSRVs != null)
                        foreach (var srv in shader.localSRVs)
                            ShaderAccessTypes.Add(ResourceAccessType.SRVTable);
                    rtpso.localShaderAccessTypes = ShaderAccessTypes.ToArray();
                    return rtpso;
                }
                catch (Exception e)
                {
                    Console.WriteLine(path);
                    Console.WriteLine(e);
                    return null;
                }
            });
        }

        public PSO GetPSOWithKeywords(IReadOnlyList<(string, string)> keywords, string path, bool enableVS = true, bool enablePS = true, bool enableGS = false)
        {
            string xPath;
            if (keywords != null)
            {
                //keywords.Sort((x, y) => x.CompareTo(y));
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(path);
                foreach (var keyword in keywords)
                {
                    stringBuilder.Append(keyword.Item1);
                    stringBuilder.Append(keyword.Item2);
                }
                stringBuilder.Append(enableVS);
                stringBuilder.Append(enablePS);
                stringBuilder.Append(enableGS);
                xPath = stringBuilder.ToString();
            }
            else
            {
                xPath = path;
            }
            return GetT(PipelineStateObjects, xPath, path, file =>
            {
                try
                {
                    string source = File.ReadAllText(file.FullName);
                    DxcDefine[] dxcDefines = null;
                    if (keywords != null)
                    {
                        dxcDefines = new DxcDefine[keywords.Count];
                        for (int i = 0; i < keywords.Count; i++)
                        {
                            dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                        }
                    }
                    byte[] vs = enableVS ? LoadShader(DxcShaderStage.Vertex, source, "vsmain", path, dxcDefines) : null;
                    byte[] gs = enableGS ? LoadShader(DxcShaderStage.Geometry, source, "gsmain", path, dxcDefines) : null;
                    byte[] ps = enablePS ? LoadShader(DxcShaderStage.Pixel, source, "psmain", path, dxcDefines) : null;
                    PSO pso = new PSO(vs, gs, ps);
                    return pso;
                }
                catch (Exception e)
                {
                    Console.WriteLine(path);
                    Console.WriteLine(e);
                    return null;
                }
            });
        }

        static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines = null)
        {
            var shaderModel = shaderStage == DxcShaderStage.Library ? DxcShaderModel.Model6_3 : DxcShaderModel.Model6_0;
            var options = new DxcCompilerOptions() { ShaderModel = shaderModel };
            var result = DxcCompiler.Compile(shaderStage, shaderCode, entryPoint, options, fileName, dxcDefines, null);
            if (result.GetStatus() != SharpGen.Runtime.Result.Ok)
            {
                string err = result.GetErrors();
                result.Dispose();
                throw new Exception(err);
            }
            byte[] resultData = result.GetResult().ToArray();
            result.Dispose();
            return resultData;
        }

        public Texture2D GetTexture(string s)
        {
            if (TextureCaches.TryGetValue(s, out var tex))
            {
                return tex.texture2D;
            }
            return null;
        }

        public TextureCube GetTextureCube(string s)
        {
            if (TextureCubes.TryGetValue(s, out var tex))
            {
                return tex;
            }
            return null;
        }

        public RootSignature GetRootSignature(string s)
        {
            if (RootSignatures.TryGetValue(s, out RootSignature rs))
                return rs;
            rs = new RootSignature();
            rs.Reload(RSFromString(s));
            RootSignatures[s] = rs;
            return rs;
        }
        static ResourceAccessType[] RSFromString(string s)
        {
            ResourceAccessType[] desc = new ResourceAccessType[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                desc[i] = c switch
                {
                    'C' => ResourceAccessType.CBV,
                    'c' => ResourceAccessType.CBVTable,
                    'S' => ResourceAccessType.SRV,
                    's' => ResourceAccessType.SRVTable,
                    'U' => ResourceAccessType.UAV,
                    'u' => ResourceAccessType.UAVTable,
                    _ => throw new NotImplementedException("error root signature desc."),
                };
            }
            return desc;
        }

        public bool TryGetTexture(string s, out Texture2D tex)
        {
            bool result = TextureCaches.TryGetValue(s, out var tex1);
            tex = tex1?.texture2D;
            if (!result)
            {
                if (Path.IsPathFullyQualified(s))
                    PreloadTexture(s);
                else
                    Console.WriteLine(s);
            }
            return result;
        }

        bool InitFolder(string path)
        {
            if (path == null) return false;
            if (KnownFolders.ContainsKey(path)) return true;
            if (!path.Contains('\\')) return false;

            var path1 = path.Substring(0, path.LastIndexOf('\\'));
            if (InitFolder(path1))
            {
                if (AddChildFolder(path) != null)
                    return true;
                return false;
            }
            else
                return false;
        }

        public DirectoryInfo AddChildFolder(string path)
        {
            try
            {
                var path1 = path.Substring(0, path.LastIndexOf('\\'));
                var folder1 = new DirectoryInfo(path);
                if (folder1 != null)
                    KnownFolders[path] = folder1;
                return folder1;
            }
            catch
            {
                return null;
            }
        }

        public static T ReadJsonStream<T>(Stream stream)
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
            using StreamReader reader1 = new StreamReader(stream);
            return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
        }

        public void Dispose()
        {
            foreach (var m in ModelPackCaches)
            {
                m.Value.Dispose();
            }
            ModelPackCaches.Clear();
            foreach (var t in TextureCaches)
            {
                t.Value.texture2D.Dispose();
            }
            TextureCaches.Clear();
            foreach (var t in ComputeShaders)
            {
                t.Value?.Dispose();
            }
            ComputeShaders.Clear();
            foreach (var t in PipelineStateObjects)
            {
                t.Value?.Dispose();
            }
            PipelineStateObjects.Clear();
            foreach (var rs in RootSignatures)
            {
                rs.Value.Dispose();
            }
            RootSignatures.Clear();
            foreach (var rtc in RTPSOs)
            {
                rtc.Value?.Dispose();
            }
            RTPSOs.Clear();
        }
    }
}
