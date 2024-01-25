using Caprice.Attributes;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using Newtonsoft.Json;
using RenderPipelines.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;

namespace RenderPipelines;

public class RenderHelper
{
    LinearPool<Mesh> meshPool = new();
    public byte[] bigBuffer = new byte[0];

    public Mesh quadMesh = new Mesh();
    public Mesh cubeMesh = new Mesh();

    Dictionary<MMDRendererComponent, int> findRenderer = new();
    public List<CBuffer> boneMatrice = new();
    public Dictionary<MMDRendererComponent, Mesh> meshOverrides = new();

    public RenderWrap renderWrap;

    public bool CPUSkinning;

    bool resourcesInitialized;

    CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
    {
        return boneMatrice[findRenderer[rendererComponent]];
    }

    public IEnumerable<MMDRendererComponent> MMDRenderers => renderWrap.rpc.renderers;


    public IEnumerable<MeshRenderable> MeshRenderables(bool setMesh = true)
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var graphicsContext = rpc.graphicsContext;
        foreach (var renderer in rpc.renderers)
        {
            var model = renderer.model;
            var mesh = model.GetMesh();
            var meshOverride = meshOverrides[renderer];
            meshOverride.baseMesh = mesh;
            if (setMesh)
            {
                graphicsContext.SetMesh(meshOverride);
                graphicsContext.SetCBVRSlot(0, GetBoneBuffer(renderer));
            }
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                var renderable = new MeshRenderable()
                {
                    mesh = meshOverride ?? mesh,
                    transform = renderer.LocalToWorld,
                    gpuSkinning = renderer.skinning && !CPUSkinning,
                    material = material,
                };
                WriteRenderable1(ref renderable, submesh);
                yield return renderable;
            }
        }
        foreach (var renderer in rpc.meshRenderers)
        {
            var model = renderer.model;
            var mesh = model.GetMesh();
            if (setMesh)
                graphicsContext.SetMesh(mesh);
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                var renderable = new MeshRenderable()
                {
                    mesh = mesh,
                    transform = renderer.transform.GetMatrix(),
                    gpuSkinning = false,
                    material = material,
                };
                WriteRenderable1(ref renderable, submesh);
                yield return renderable;
            }
        }
    }

    void WriteRenderable1(ref MeshRenderable renderable, Submesh submesh)
    {
        renderable.indexStart = submesh.indexOffset;
        renderable.indexCount = submesh.indexCount;
        renderable.vertexStart = submesh.vertexStart;
        renderable.vertexCount = submesh.vertexCount;
        renderable.drawDoubleFace = submesh.DrawDoubleFace;
    }

    void InitializeResources()
    {
        resourcesInitialized = true;
        quadMesh.LoadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
        cubeMesh.LoadIndex<int>(4, new int[]
        {
            0,1,2,
            2,1,3,
            0,2,4,
            2,6,4,
            1,5,7,
            3,1,7,
            2,3,7,
            2,7,6,
            1,0,4,
            1,4,5,
            4,7,5,
            4,6,7,
        });
        var graphicsContext = renderWrap.graphicsContext;
        graphicsContext.UploadMesh(quadMesh);
        graphicsContext.UploadMesh(cubeMesh);
    }

    public void UpdateGPUResource()
    {
        Writer.graphicsContext = renderWrap.graphicsContext;
        Writer.Clear();
        if (!resourcesInitialized)
            InitializeResources();
        UpdateBoneMatrice();
        if (!CPUSkinning)
            Morph();
        else
            SkinAndMorph();
    }

    void UpdateBoneMatrice()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;
        var graphicsContext = rpc.graphicsContext;

        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].WriteMatriticesData();
        }
        findRenderer.Clear();
        while (boneMatrice.Count < renderers.Count)
        {
            boneMatrice.Add(new CBuffer());
        }
        Span<Matrix4x4> matrice = stackalloc Matrix4x4[1024];
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var matrices = renderer.BoneMatricesData;
            int l = Math.Min(matrices.Length, 1024);
            for (int k = 0; k < l; k++)
                matrice[k] = Matrix4x4.Transpose(matrices[k]);
            graphicsContext.UpdateResource<Matrix4x4>(boneMatrice[i], matrice.Slice(0, l));
            findRenderer[renderer] = i;
        }
    }

    void CheckBigBuffer()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;
        int bufferSize = 0;
        foreach (var renderer in renderers)
        {
            if (renderer.skinning)
                bufferSize = Math.Max(renderer.model.vertexCount, bufferSize);
        }
        bufferSize *= 24;
        if (bufferSize > bigBuffer.Length)
            bigBuffer = new byte[bufferSize];
    }

    void Morph()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;
        var graphicsContext = rpc.graphicsContext;
        meshPool.Reset();
        meshOverrides.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var model = renderer.model;
            var mesh = meshPool.Get(() => new Mesh());
            mesh.LoadIndex<int>(model.vertexCount, null);
            meshOverrides[renderer] = mesh;
            if (!renderer.skinning)
                continue;

            graphicsContext.UpdateMeshOneFrame<Vector3>(mesh, renderer.MeshPosition, MeshRenderable.POSITION);
            graphicsContext.EndUpdateMesh(mesh);
        }
    }

    public void CPUOnlySkinning()
    {
        if (!resourcesInitialized)
            InitializeResources();
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;
        var graphicsContext = rpc.graphicsContext;
        meshPool.Reset();
        meshOverrides.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].WriteMatriticesData();
        }

        CheckBigBuffer();

        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var model = renderer.model;
            var mesh = meshPool.Get(() => new Mesh());
            mesh.LoadIndex<int>(model.vertexCount, null);
            meshOverrides[renderer] = mesh;
            if (!renderer.skinning)
                continue;

            Skinning(model, renderer, mesh);
        }
    }

    void SkinAndMorph()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;
        var graphicsContext = rpc.graphicsContext;
        meshPool.Reset();
        meshOverrides.Clear();

        CheckBigBuffer();

        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var model = renderer.model;
            var mesh = meshPool.Get(() => new Mesh());
            mesh.LoadIndex<int>(model.vertexCount, null);
            meshOverrides[renderer] = mesh;
            if (!renderer.skinning)
                continue;

            Skinning(model, renderer, mesh);
            graphicsContext.UpdateMeshOneFrame(mesh);
        }
    }

    void Skinning(ModelPack model, MMDRendererComponent renderer, Mesh mesh)
    {
        var rangePartitioner = Partitioner.Create(0, model.vertexCount);
        int halfLength = bigBuffer.Length / 12 / 2;
        Parallel.ForEach(rangePartitioner, (range, loopState) =>
        {
            Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12 / 2));
            Span<Vector3> _d4 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, bigBuffer.Length / 12 * 12 / 2, bigBuffer.Length / 12 * 12 / 2));
            int from = range.Item1;
            int to = range.Item2;
            for (int j = from; j < to; j++)
            {
                int k;
                Matrix4x4 final = new Matrix4x4();
                for (k = 0; k < 4; k++)
                {
                    int boneId = model.boneId[j * 4 + k];
                    if (boneId >= renderer.bones.Count)
                        break;
                    float weight = model.boneWeights[j * 4 + k];
                    final += renderer.BoneMatricesData[boneId] * weight;
                }
                Vector3 pos0 = renderer.MeshPosition[j];
                Vector3 pos1 = Vector3.Transform(pos0, final);
                if (k > 0)
                    _d3[j] = pos1;
                else
                    _d3[j] = pos0;
                Vector3 norm0 = model.normal[j];
                Vector3 norm1 = Vector3.TransformNormal(norm0, final);
                if (k > 0)
                    _d4[j] = Vector3.Normalize(norm1);
                else
                    _d4[j] = Vector3.Normalize(norm0);
            }
        });
        Span<Vector3> dat0 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));

        mesh.AddBuffer<Vector3>(dat0.Slice(0, model.vertexCount), MeshRenderable.POSITION);
        mesh.AddBuffer<Vector3>(dat0.Slice(halfLength, model.vertexCount), MeshRenderable.NORMAL);
    }

    public void DrawQuad(int instanceCount = 1)
    {
        if (!resourcesInitialized)
            InitializeResources();
        var graphicsContext = renderWrap.graphicsContext;
        graphicsContext.SetMesh(quadMesh);
        graphicsContext.DrawIndexedInstanced(6, instanceCount, 0, 0, 0);
    }

    public void DrawCube(int instanceCount = 1)
    {
        if (!resourcesInitialized)
            InitializeResources();
        var graphicsContext = renderWrap.graphicsContext;
        graphicsContext.SetMesh(cubeMesh);
        graphicsContext.DrawIndexedInstanced(36, instanceCount, 0, 0, 0);
    }

    public void Draw(MeshRenderable renderable)
    {
        renderWrap.graphicsContext.DrawIndexed(renderable.indexCount, renderable.indexStart, renderable.vertexStart);
    }

    #region write object
    public object GetIndexableValue(string key)
    {
        for (int i = dataStack.Count - 1; i >= 0; i--)
        {
            var finder = dataStack[i];
            if (finder.Item2.TryGetValue(key, out var memberInfo1))
            {
                return memberInfo1.GetValue<object>(finder.Item1);
            }
        }
        return null;
    }

    public object GetIndexableValue(string key, RenderMaterial material)
    {
        var obj1 = material.GetObject(key);
        if (obj1 != null && paramBaseType.TryGetValue(key, out var type))
        {
            var objType = obj1.GetType();
            if (objType == type)
                return obj1;
            else if (type.IsEnum && objType == typeof(string))
            {
                if (Enum.TryParse(type, (string)obj1, out var obj2))
                {
                    return obj2;
                }
            }
        }
        for (int i = dataStack.Count - 1; i >= 0; i--)
        {
            var finder = dataStack[i];
            if (finder.Item2.TryGetValue(key, out var memberInfo1))
            {
                return memberInfo1.GetValue<object>(finder.Item1);
            }
        }
        return null;
    }

    List<(object, Dictionary<string, MemberInfo>)> dataStack = new();
    Dictionary<Type, Dictionary<string, MemberInfo>> memberInfoCache = new();
    Dictionary<string, Type> paramBaseType = new();

    public GPUWriter Writer = new();


    public void PushParameters(object parameterProvider)
    {
        var type = parameterProvider.GetType();

        if (!memberInfoCache.TryGetValue(type, out var parameters))
        {
            parameters = new();
            foreach (var member in type.GetMembers())
            {
                var indexableAttribute = member.GetCustomAttribute<IndexableAttribute>();
                if (indexableAttribute != null)
                {
                    parameters[indexableAttribute.Name ?? member.Name] = member;
                }
            }
            memberInfoCache[type] = parameters;
        }

        if (paramBaseType.Count == 0)//for optimise performance
        {
            paramBaseType.Clear();
            foreach (var pair in parameters)
            {
                paramBaseType[pair.Key] = pair.Value.GetGetterType();
            }
        }

        dataStack.Add((parameterProvider, parameters));
    }

    public void PopParameters()
    {
        dataStack.RemoveAt(dataStack.Count - 1);
    }

    public void Write(object[] datas, GPUWriter writer, RenderMaterial material = null)
    {
        foreach (var obj in datas)
            WriteObject(obj, writer, material?.Parameters);
    }

    public void Write(ReadOnlySpan<object> datas, GPUWriter writer, RenderMaterial material = null)
    {
        foreach (var obj in datas)
            WriteObject(obj, writer, material.Parameters);
    }

    void WriteObject(object obj, GPUWriter writer, IDictionary<string, object> material = null)
    {
        if (obj is string s)
        {
            if (paramBaseType.TryGetValue(s, out var type) && material != null)
            {
                if (material.TryGetValue(s, out object obj1) && obj1.GetType() == type)
                {
                    writer.WriteObject(obj1);
                    return;
                }
            }
            for (int i = dataStack.Count - 1; i >= 0; i--)
            {
                var finder = dataStack[i];
                if (finder.Item2.TryGetValue(s, out var memberInfo1))
                {
                    writer.WriteObject(memberInfo1.GetValue<object>(finder.Item1));
                    return;
                }
            }
        }
        else
        {
            writer.WriteObject(obj);
        }
    }

    void SetCBV(GPUWriter writer, int slot)
    {
        renderWrap.graphicsContext.SetCBVRSlot(slot, new ReadOnlySpan<byte>(writer.memoryStream.GetBuffer(), 0, (int)writer.memoryStream.Position));
        writer.Seek(0, SeekOrigin.Begin);
    }

    static byte[] LoadShader(DxcShaderStage shaderStage, string shaderCode, string entryPoint, string fileName, DxcDefine[] dxcDefines, out ID3D12ShaderReflection reflection)
    {
        var shaderModel = DxcShaderModel.Model6_0;
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

    public static ComputeShader CreateComputeShader(string source, string entry, string fileName = null)
    {
        var cs = LoadShader(DxcShaderStage.Compute, source, entry, fileName, null, out var reflection);
        return new ComputeShader(cs, reflection);
    }

    public static ComputeShader CreateComputeShader<T>(string source, string entry, T e, string fileName = null) where T : struct, Enum
    {
        var defs = GetDxcDefines(e);
        var cs = LoadShader(DxcShaderStage.Compute, source, entry, fileName, defs, out var reflection);
        return new ComputeShader(cs, reflection);
    }

    public static PSO CreatePipeline(string source, string vsEntry, string gsEntry, string psEntry, string fileName = null)
    {
        ID3D12ShaderReflection vsr = null;
        ID3D12ShaderReflection gsr = null;
        ID3D12ShaderReflection psr = null;
        var vs = vsEntry != null ? LoadShader(DxcShaderStage.Vertex, source, vsEntry, fileName, null, out vsr) : null;
        var gs = gsEntry != null ? LoadShader(DxcShaderStage.Geometry, source, gsEntry, fileName, null, out gsr) : null;
        var ps = psEntry != null ? LoadShader(DxcShaderStage.Pixel, source, psEntry, fileName, null, out psr) : null;

        return new PSO(vs, gs, ps, vsr, gsr, psr);
    }

    public static PSO CreatePipeline<T>(string source, string vsEntry, string gsEntry, string psEntry, T e, string fileName = null) where T : struct, Enum
    {
        ID3D12ShaderReflection vsr = null;
        ID3D12ShaderReflection gsr = null;
        ID3D12ShaderReflection psr = null;
        var defs = GetDxcDefines(e);
        var vs = vsEntry != null ? LoadShader(DxcShaderStage.Vertex, source, vsEntry, fileName, defs, out vsr) : null;
        var gs = gsEntry != null ? LoadShader(DxcShaderStage.Geometry, source, gsEntry, fileName, defs, out gsr) : null;
        var ps = psEntry != null ? LoadShader(DxcShaderStage.Pixel, source, psEntry, fileName, defs, out psr) : null;
        return new PSO(vs, gs, ps, vsr, gsr, psr);
    }

    static DxcDefine[] GetDxcDefines<T>(T e) where T : struct, Enum
    {
        var arr = Enum.GetValues<T>();
        var defs = new List<DxcDefine>();
        foreach (var a in arr)
        {
            if (Convert.ToInt32(a) == 0)
                continue;
            if (e.HasFlag(a))
                defs.Add(new DxcDefine
                {
                    Name = a.ToString(),
                    Value = "1"
                });
        }
        return defs.ToArray();
    }

    //public static void TestEnum<T>(T e) where T : struct, Enum
    //{
    //    var arr = Enum.GetValues<T>();
    //    foreach (var a in arr)
    //    {
    //        if (Convert.ToInt32(a) == 0)
    //            continue;
    //        if (e.HasFlag(a))
    //            Console.WriteLine($"has {a}.");
    //        Console.WriteLine($"{a}, {Convert.ToInt32(a)}");
    //    }
    //}
    #endregion

    #region RenderResource
    public VersionedDictionary<string, RTPSO> RTPSOs = new();

    public RTPSO GetRTPSO(IReadOnlyList<(string, string)> keywords, RayTracingShader shader, string path)
    {
        string xPath;
        if (keywords != null)
        {
            //keywords.Sort((x, y) => x.CompareTo(y));
            var stringBuilder = new StringBuilder();
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
    public T GetT<T>(VersionedDictionary<string, T> caches, string path, string realPath, Func<FileInfo, T> createFun) where T : class
    {
        if (!caches.TryGetValue(path, out var file))
        {
            try
            {
                FileInfo fileInfo = new FileInfo(realPath);
                file = createFun(fileInfo);
                caches[path] = file;
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

    public static T ReadJsonStream<T>(Stream stream)
    {
        JsonSerializer jsonSerializer = new JsonSerializer();
        jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
        using StreamReader reader1 = new StreamReader(stream);
        return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
    }

    #endregion
    public void Dispose()
    {
        findRenderer.Clear();
        meshOverrides.Clear();
        quadMesh?.Dispose();
        cubeMesh?.Dispose();
        foreach (var obj in meshPool.list1)
        {
            obj.Dispose();
        }
        foreach (var rtc in RTPSOs)
        {
            rtc.Value?.Dispose();
        }
        RTPSOs.Clear();
    }
}
