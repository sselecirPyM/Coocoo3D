using Coocoo3D.Components;
using Coocoo3D.Core;
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

    public VersionedDictionary<string, PSO> PipelineStateObjects = new();
    public VersionedDictionary<string, Assembly> Assemblies = new();

    List<IResourceLoader<ModelPack>> modelLoaders = new List<IResourceLoader<ModelPack>>();
    public Dictionary<string, ModelPack> modelCaches = new Dictionary<string, ModelPack>();
    List<IResourceLoader<Texture2D>> texture2DLoaders = new List<IResourceLoader<Texture2D>>();
    public Dictionary<string, Texture2D> textureCaches = new Dictionary<string, Texture2D>();
    List<IResourceLoader<MMDMotion>> motionLoaders = new List<IResourceLoader<MMDMotion>>();
    public Dictionary<string, MMDMotion> motionCaches = new Dictionary<string, MMDMotion>();

    public GameDriverContext gameDriverContext;

    public EngineContext engineContext;

    public GraphicsContext graphicsContext1;

    ConcurrentQueue<AsyncProxy> asyncProxyQueue = new ConcurrentQueue<AsyncProxy>();
    ConcurrentQueue<Action> syncProxyQueue = new ConcurrentQueue<Action>();

    List<AsyncProxy> runningTasks = new List<AsyncProxy>();

    public string workDir = System.Environment.CurrentDirectory;
    public MainCaches()
    {

    }

    public void Initialize1()
    {
        foreach (var resourceLoader in engineContext.extensionFactory.ResourceLoaders)
        {
            if (resourceLoader is IResourceLoader<Texture2D> te)
            {
                texture2DLoaders.Add(te);
            }
            if (resourceLoader is IResourceLoader<MMDMotion> mo)
            {
                motionLoaders.Add(mo);
            }
            if (resourceLoader is IResourceLoader<ModelPack> modelLoader)
            {
                modelLoaders.Add(modelLoader);
            }
        }
    }

    public void _ReloadShaders()
    {
        foreach (var knownFile in KnownFiles)
        {
            knownFile.Value.requireReload = true;
        }
        Console.Clear();
    }

    public void ProxyCall(AsyncProxy proxy)
    {
        asyncProxyQueue.Enqueue(proxy);
    }

    public void ProxyCall(Action call)
    {
        syncProxyQueue.Enqueue(call);
    }

    Queue<string> textureLoadQueue = new();
    public void OnFrame()
    {
        long cost = 0;
        runningTasks.RemoveAll((t) =>
        {
            cost += t.cost;
            if (t.runningTask == null || t.runningTask.Status != System.Threading.Tasks.TaskStatus.Running)
            {
                return true;
            }
            return false;
        });

        while (cost < 8 && asyncProxyQueue.TryDequeue(out var proxy))
        {
            proxy.runningTask = proxy.calls();
            runningTasks.Add(proxy);
            cost += proxy.cost;
        }
        while (syncProxyQueue.TryDequeue(out var call))
        {
            call();
        }
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


    public Texture2D GetTextureLoaded(string path)
    {
        path = Path.GetFullPath(path);
        if (string.IsNullOrEmpty(path))
            return null;
        if (textureCaches.TryGetValue(path, out var val))
        {
            return val;
        }
        try
        {
            var texture2D = new Texture2D();
            var uploader = new Uploader();
            using var stream = File.OpenRead(path);
            Texture2DPack.LoadTexture(path, stream, uploader);
            graphicsContext1.UploadTexture(texture2D, uploader);
            texture2D.Status = GraphicsObjectStatus.loaded;
            textureCaches[path] = texture2D;

            return texture2D;
        }
        catch
        {

        }
        return null;
    }

    public ModelPack GetModel(string path)
    {
        if (modelCaches.TryGetValue(path, out var model))
        {
            return model;
        }
        foreach (var loader in modelLoaders)
        {
            try
            {
                if (loader.TryLoad(path, out var value))
                {
                    modelCaches[path] = value;
                    return value;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        modelCaches[path] = null;
        return null;
    }

    public MMDMotion GetMotion(string path)
    {
        path = Path.GetFullPath(path, workDir);
        if (motionCaches.TryGetValue(path, out var motion))
        {
            return motion;
        }
        foreach (var loader in motionLoaders)
        {
            try
            {
                if (loader.TryLoad(path, out var value))
                {
                    motionCaches[path] = value;
                    return value;
                }
            }
            catch
            {

            }
        }
        motionCaches[path] = null;
        return null;
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

    public Texture2D GetTexturePreloaded(string path)
    {
        if (textureCaches.TryGetValue(path, out var texture))
        {
            return texture;
        }
        foreach (var loader in texture2DLoaders)
        {
            if (loader.TryLoad(path, out var value))
            {
                textureCaches[path] = value;
                return value;
            }
        }
        textureCaches[path] = null;
        return null;
    }

    public void Dispose()
    {
        foreach (var t in PipelineStateObjects)
        {
            t.Value?.Dispose();
        }
        PipelineStateObjects.Clear();

        motionCaches.Clear();

        foreach (var t in textureCaches)
        {
            t.Value?.Dispose();
        }
        textureCaches.Clear();
        foreach (var t in modelCaches)
        {
            t.Value?.Dispose();
        }
        modelCaches.Clear();
    }
}
