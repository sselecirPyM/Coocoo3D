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

    public VersionedDictionary<string, PSO> PipelineStateObjects = new();
    public VersionedDictionary<string, Assembly> Assemblies = new();

    public ConcurrentQueue<Mesh> MeshReadyToUpload = new();

    public TextureDecodeHandler textureDecodeHandler = new();
    public CacheHandler cacheHandler = new();
    public SyncHandler<GpuUploadTask> uploadHandler = new();
    public SyncHandler<ModelLoadTask> modelLoadHandler = new();
    public SyncHandler<SceneLoadTask> sceneLoadHandler = new();
    public SyncHandler<SceneSaveTask> sceneSaveHandler = new();

    public GameDriverContext gameDriverContext;

    string workDir = System.Environment.CurrentDirectory;
    public MainCaches()
    {
        textureDecodeHandler.LoadComplete = () => gameDriverContext.RequireRender(true);
    }

    public void _ReloadShaders()
    {
        foreach (var knownFile in KnownFiles)
        {
            knownFile.Value.requireReload = true;
        }
        Console.Clear();
    }

    public void _ReloadTextures()
    {
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

    public void PreloadTexture(string fullPath)
    {
        if (!TextureOnDemand.ContainsKey(fullPath))
            TextureOnDemand[fullPath] = false;
    }

    Queue<string> textureLoadQueue = new();
    public void OnFrame(GraphicsContext graphicsContext)
    {
        cacheHandler.mainCaches = this;
        sceneLoadHandler.state = this;
        modelLoadHandler.state = this;
        modelLoadHandler.maxProcessingCount = 8;

        HandlerUpdate1(sceneLoadHandler);
        HandlerUpdate1(sceneSaveHandler);
        HandlerUpdate1(modelLoadHandler);

        foreach (var notLoad in TextureOnDemand)
        {
            if (!notLoad.Value)
                textureLoadQueue.Enqueue(notLoad.Key);
        }

        while (textureLoadQueue.TryDequeue(out var key))
        {
            TextureOnDemand[key] = true;
            var texturePack = TextureCaches.GetOrCreate(key);
            texturePack.fullPath = key;

            TextureReplace(texturePack.texture2D, graphicsContext);
            var task = new TextureLoadTask(texturePack);
            task.KnownFile = GetFileInfo(key);
            cacheHandler.Add(task);
        }
        HandlerUpdate(cacheHandler, graphicsContext);

        textureDecodeHandler.Update();

        textureDecodeHandler.Output.RemoveAll(task1 =>
        {
            if (uploadHandler.inputs.Count > 3)
                return false;
            var task = (TextureLoadTask)task1;

            if (task.Uploader != null)
            {
                uploadHandler.Add(new GpuUploadTask(task.TexturePack.texture2D, task.Uploader));
            }
            else
            {
                task.TexturePack.texture2D.Status = task.TexturePack.Status;
                TextureReplace(task.TexturePack.texture2D, graphicsContext);
            }
            TextureOnDemand.Remove(task.TexturePack.fullPath);
            return true;
        });


        while (MeshReadyToUpload.TryDequeue(out var mesh))
            graphicsContext.UploadMesh(mesh);

        uploadHandler.maxProcessingCount = 10;
        uploadHandler.state = graphicsContext;
        uploadHandler.Update();
        uploadHandler.Output.Clear();
    }

    void HandlerUpdate(CacheHandler handler, GraphicsContext graphicsContext)
    {
        handler.Update();
        if (handler.Output.Count > 0)
            gameDriverContext.RequireRender(true);

        foreach (var task in handler.Output)
        {
            if (task.Next == null)
            {
                task.OnLeavePipeline();

                TextureReplace(task.TexturePack.texture2D, graphicsContext);
                TextureOnDemand.Remove(task.TexturePack.fullPath);
            }
            else if (task.Next == "ITextureDecodeTask")
            {
                task.OnEnterPipeline();
                TextureReplace(task.TexturePack.texture2D, graphicsContext);
                textureDecodeHandler.Add(task);
            }
        }

        handler.Output.Clear();
    }

    void HandlerUpdate1<T>(SyncHandler<T> handler) where T : ISyncTask
    {
        handler.Update();
        if (handler.Output.Count > 0)
            gameDriverContext.RequireRender(true);

        handler.Output.Clear();
    }

    void TextureReplace(Texture2D texture, GraphicsContext graphicsContext)
    {
        if (texture.Status == GraphicsObjectStatus.loading)
            GetTextureLoaded1("Assets/Textures/loading.png", graphicsContext).RefCopyTo(texture);
        else if (texture.Status != GraphicsObjectStatus.loaded)
            GetTextureLoaded1("Assets/Textures/error.png", graphicsContext).RefCopyTo(texture);
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
    Texture2D GetTextureLoaded1(string path, GraphicsContext graphicsContext)
    {
        return GetTextureLoaded(Path.GetFullPath(path, workDir), graphicsContext);
    }


    public Texture2D GetTextureLoaded(string path, GraphicsContext graphicsContext)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        var a = GetT(TextureCaches, path, file =>
        {
            var texturePack1 = new Texture2DPack();
            texturePack1.fullPath = path;
            Uploader uploader = new Uploader();
            using var stream = file.OpenRead();
            Texture2DPack.LoadTexture(file.FullName, stream, uploader);
            graphicsContext.UploadTexture(texturePack1.texture2D, uploader);
            texturePack1.Status = GraphicsObjectStatus.loaded;
            texturePack1.texture2D.Status = GraphicsObjectStatus.loaded;
            return texturePack1;
        });
        return a.texture2D;
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

    public PSO GetPSO(IReadOnlyList<(string, string)> keywords, string path)
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

        foreach (var t in PipelineStateObjects)
        {
            t.Value?.Dispose();
        }
        PipelineStateObjects.Clear();
    }
}
