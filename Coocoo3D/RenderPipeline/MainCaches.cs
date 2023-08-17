using Coocoo3D.Components;
using Coocoo3D.Core;
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
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;

namespace Coocoo3D.RenderPipeline;

public class MainCaches : IDisposable
{
    public Dictionary<string, KnownFile> KnownFiles = new();

    public VersionedDictionary<string, Texture2DPack> TextureCaches = new();
    Dictionary<string, bool> TextureOnDemand = new();

    public VersionedDictionary<string, ModelPack> ModelPackCaches = new();
    public VersionedDictionary<string, MMDMotion> Motions = new();
    public VersionedDictionary<string, ComputeShader> ComputeShaders = new();

    public VersionedDictionary<string, RayTracingShader> RayTracingShaders = new();
    public VersionedDictionary<string, PSOEx> PipelineStateObjects = new();
    public VersionedDictionary<string, RTPSO> RTPSOs = new();
    public VersionedDictionary<string, Assembly> Assemblies = new();

    public ConcurrentQueue<Mesh> MeshReadyToUpload = new();

    public DiskLoadHandler diskLoadHandler = new();
    public TextureDecodeHandler textureDecodeHandler = new();
    public CacheHandler cacheHandler = new();
    public SyncHandler<GpuUploadTask> uploadHandler = new();
    public SyncHandler<ModelLoadTask> modelLoadHandler = new();
    public SyncHandler<SceneLoadTask> sceneLoadHandler = new();
    public SyncHandler<SceneSaveTask> sceneSaveHandler = new();

    public GameDriverContext gameDriverContext;

    public MainCaches()
    {
        textureDecodeHandler.LoadComplete = () => gameDriverContext.RequireRender(true);
        diskLoadHandler.LoadComplete = () => gameDriverContext.RequireRender(true);
    }


    public bool ReloadTextures = false;
    public bool ReloadShaders = false;

    public void PreloadTexture(string fullPath)
    {
        if (!TextureOnDemand.ContainsKey(fullPath))
            TextureOnDemand[fullPath] = false;
    }

    Queue<string> textureLoadQueue = new();
    public void OnFrame()
    {
        if (ReloadShaders)
        {
            ReloadShaders = false;
            foreach (var knownFile in KnownFiles)
            {
                knownFile.Value.requireReload = true;
            }
            Console.Clear();
        }
        if (ReloadTextures)
        {
            ReloadTextures = false;
            var packs = TextureCaches.ToList();
            foreach (var pair in packs)
            {
                TextureOnDemand.TryAdd(pair.Key, false);
            }
            foreach (var pair in KnownFiles)
            {
                pair.Value.requireReload = true;
            }
            Console.Clear();
        }
        cacheHandler.mainCaches = this;
        sceneLoadHandler.state = this;
        modelLoadHandler.state = this;
        modelLoadHandler.maxProcessingCount = 8;

        HandlerUpdate(sceneLoadHandler);
        HandlerUpdate(sceneSaveHandler);
        HandlerUpdate(modelLoadHandler);

        foreach (var notLoad in TextureOnDemand)
        {
            if (!notLoad.Value)
                textureLoadQueue.Enqueue(notLoad.Key);
        }

        while (textureLoadQueue.TryDequeue(out var key))
        {
            TextureOnDemand[key] = true;
            var tex1 = TextureCaches.GetOrCreate(key);
            var task = new TextureLoadTask(tex1);
            tex1.fullPath = key;
            task.KnownFile = GetFileInfo(key);
            cacheHandler.Add(task);
        }
        HandlerUpdate(cacheHandler);
        HandlerUpdate(diskLoadHandler);

        textureDecodeHandler.Update();

        textureDecodeHandler.Output.RemoveAll(task1 =>
        {
            if (uploadHandler.inputs.Count > 3)
                return false;
            var task = (TextureLoadTask)task1;
            if (task.texture != null && task.Uploader == null)
                task.texture.Status = task.TexturePack.Status;
            if (task.Uploader != null)
                uploadHandler.Add(new GpuUploadTask(task.Texture, task.Uploader));
            TextureOnDemand.Remove(task.TexturePack.fullPath);
            return true;
        });
    }

    void HandlerUpdate<T>(IHandler<T> handler)
    {
        handler.Update();
        if (handler.Output.Count > 0)
            gameDriverContext.RequireRender(true);

        foreach (var task in handler.Output)
        {
            if (task is TextureLoadTask navigableTask)
            {
                PipelineNavigation(navigableTask);
            }
        }

        handler.Output.Clear();
    }

    void PipelineNavigation(TextureLoadTask navigableTask)
    {
        if (navigableTask.Next == null)
        {
            navigableTask.OnLeavePipeline();
            if (navigableTask is TextureLoadTask textureLoadTask)
            {
                TextureOnDemand.Remove(textureLoadTask.TexturePack.fullPath);
            }
        }
        else if (navigableTask.Next == "IDiskLoadTask")
            AddTask(diskLoadHandler, navigableTask, "IDiskLoadTask");
        else if (navigableTask.Next == "ITextureDecodeTask")
            AddTask(textureDecodeHandler, navigableTask, "ITextureDecodeTask");
    }

    bool AddTask(IHandler<TextureLoadTask> handler, TextureLoadTask task, string type)
    {
        bool r = handler.Add(task);
        if (r)
        {
            switch (type)
            {
                case "IDiskLoadTask":
                    task.Next = "ITextureDecodeTask";
                    break;
                default:
                    task.Next = null;
                    break;
            }
        }
        return r;
    }

    public KnownFile GetFileInfo(string path)
    {
        return KnownFiles.GetOrCreate(path, () => new KnownFile()
        {
            fullPath = path,
        });
    }

    public T GetT<T>(VersionedDictionary<string, T> caches, string path, Func<FileInfo, T> createFun) where T : class
    {
        return GetT(caches, path, path, createFun);
    }
    public T GetT<T>(VersionedDictionary<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
    {
        var knownFile = GetFileInfo(realPath);
        int modifyCount = knownFile.modifiyCount;
        if (knownFile.requireReload || knownFile.file == null)
        {
            knownFile.requireReload = false;
            string folderPath = Path.GetDirectoryName(realPath);
            if (!Path.IsPathRooted(folderPath))
                return null;
            var folder = new DirectoryInfo(folderPath);
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
            using var stream = file.OpenRead();
            texturePack1.LoadTexture(file.FullName, stream, uploader);
            graphicsContext.UploadTexture(texturePack1.texture2D, uploader);
            texturePack1.Status = GraphicsObjectStatus.loaded;
            texturePack1.texture2D.Status = GraphicsObjectStatus.loaded;
            return texturePack1;
        }).texture2D;
    }

    public ModelPack GetModel(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
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

                var paths = new HashSet<string>(modelPack.textures);
                foreach (var material in modelPack.Materials)
                {
                    var keys = new List<string>(material.Parameters.Keys);
                    foreach (var key in keys)
                    {
                        object o = material.Parameters[key];
                        if (o as string == ModelPack.whiteTextureReplace)
                        {
                            material.Parameters[key] = GetTexturePreloaded(Path.GetFullPath("Assets/Textures/white.png"));
                        }
                        else if (o is string path && paths.Contains(path))
                        {
                            material.Parameters[key] = GetTexturePreloaded(path);
                        }
                    }
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
            using var stream = file.OpenRead();
            BinaryReader reader = new BinaryReader(stream);
            VMDFormat motionSet = VMDFormat.Load(reader);

            var motion = new MMDMotion();
            motion.Load(motionSet);
            return motion;
        });
    }

    public Type[] GetDerivedTypes(string path, Type baseType)
    {
        var assembly = GetAssembly(path);
        return assembly.GetTypes().Where(u => u.IsSubclassOf(baseType) && !u.IsAbstract && !u.IsGenericType).ToArray();
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
            var shaderCode = LoadShader(DxcShaderStage.Compute, File.ReadAllText(path), "csmain", path, dxcDefines, out var reflection);
            computeShader.Initialize(shaderCode, reflection);
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

    public PSOEx GetPSO(IReadOnlyList<(string, string)> keywords, string path)
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
        return GetT(PipelineStateObjects, xPath, path, file =>
        {
            try
            {
                string source = File.ReadAllText(file.FullName);
                ShaderReader shaderReader = new ShaderReader(source);
                DxcDefine[] dxcDefines = null;
                if (keywords != null)
                {
                    dxcDefines = new DxcDefine[keywords.Count];
                    for (int i = 0; i < keywords.Count; i++)
                    {
                        dxcDefines[i] = new DxcDefine() { Name = keywords[i].Item1, Value = keywords[i].Item2 };
                    }
                }
                ID3D12ShaderReflection vsr = null;
                ID3D12ShaderReflection gsr = null;
                ID3D12ShaderReflection psr = null;
                byte[] vs = shaderReader.vertexShader != null ? LoadShader(DxcShaderStage.Vertex, source, shaderReader.vertexShader, path, dxcDefines, out vsr) : null;
                byte[] gs = shaderReader.geometryShader != null ? LoadShader(DxcShaderStage.Geometry, source, shaderReader.geometryShader, path, dxcDefines, out gsr) : null;
                byte[] ps = shaderReader.pixelShader != null ? LoadShader(DxcShaderStage.Pixel, source, shaderReader.pixelShader, path, dxcDefines, out psr) : null;
                PSO pso = new PSO(vs, gs, ps, vsr, gsr, psr);
                PSOEx psoEx = new PSOEx()
                {
                    pso = pso,
                    blend = shaderReader.blend,
                };
                return psoEx;
            }
            catch (Exception e)
            {
                Console.WriteLine(path);
                Console.WriteLine(e);
                return null;
            }
        });
    }

    static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines, out ID3D12ShaderReflection reflection)
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
        byte[] resultData = result.GetResult().AsBytes();
        reflection = DxcCompiler.Utils.CreateReflection<ID3D12ShaderReflection>(result.GetOutput(DxcOutKind.Reflection));

        result.Dispose();
        return resultData;
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
        byte[] resultData = result.GetResult().AsBytes();
        result.Dispose();
        return resultData;
    }

    public bool TryGetTexture(string path, out Texture2D tex)
    {
        bool result = TextureCaches.TryGetValue(path, out var tex1);
        tex = tex1?.texture2D;
        if (!result)
        {
            if (Path.IsPathFullyQualified(path))
                PreloadTexture(path);
            else
                Console.WriteLine(path);
        }
        return result;
    }

    public Texture2D GetTexturePreloaded(string path)
    {
        bool result = TextureCaches.TryGetValue(path, out var tex1);
        if (!result)
        {
            tex1 = new Texture2DPack()
            {
                fullPath = path,
            };
            TextureCaches[path] = tex1;

            if (Path.IsPathFullyQualified(path))
                PreloadTexture(path);
            else
                Console.WriteLine(path);
        }
        //tex = tex1?.texture2D;
        return tex1.texture2D;
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
            t.Value?.Dispose();
        }
        TextureCaches.Clear();
        foreach (var t in ComputeShaders)
        {
            t.Value?.Dispose();
        }
        ComputeShaders.Clear();
        foreach (var t in PipelineStateObjects)
        {
            t.Value?.pso.Dispose();
        }
        PipelineStateObjects.Clear();
        foreach (var rtc in RTPSOs)
        {
            rtc.Value?.Dispose();
        }
        RTPSOs.Clear();
    }
}
