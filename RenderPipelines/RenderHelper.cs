using Caprice.Attributes;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using Newtonsoft.Json;
using RenderPipelines.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12.Shader;
using Vortice.Dxc;

namespace RenderPipelines;

public class RenderHelper
{
    LinearPool<Mesh> meshPool = new();

    public Mesh quadMesh = new Mesh();
    public Mesh cubeMesh = new Mesh();

    public RenderPipeline renderPipeline;

    public Dictionary<MMDRendererComponent, Mesh> meshOverrides = new();

    public RenderWrap renderWrap;

    GraphicsContext graphicsContext => renderWrap.graphicsContext;

    bool resourcesInitialized;

    public IEnumerable<MMDRendererComponent> MMDRenderers => renderWrap.rpc.renderers;


    public IEnumerable<MeshRenderable<T>> MeshRenderables<T>() where T : class, new()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        foreach (var renderer in rpc.renderers)
        {
            var model = renderer.model;
            var meshOverride = meshOverrides[renderer];
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                yield return GetRenderable<T>(submesh, meshOverride, renderer.LocalToWorld, material);
            }
        }
        foreach (var renderer in rpc.meshRenderers)
        {
            var model = renderer.model;
            var mesh = model.GetMesh();
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                yield return GetRenderable<T>(submesh, mesh, renderer.transform.GetMatrix(), material);
            }
        }
    }

    MeshRenderable<T> GetRenderable<T>(Submesh submesh, Mesh mesh, Matrix4x4 transform, RenderMaterial material) where T : class, new()
    {
        material.Type = Caprice.Display.UIShowType.Material;
        MeshRenderable<T> renderable = new MeshRenderable<T>();
        renderable.indexStart = submesh.indexOffset;
        renderable.indexCount = submesh.indexCount;
        renderable.vertexStart = submesh.vertexStart;
        renderable.vertexCount = submesh.vertexCount;
        renderable.drawDoubleFace = submesh.DrawDoubleFace;
        renderable.mesh = mesh;
        renderable.transform = transform;
        renderable.material = renderPipeline.UIMaterial(material) as T;
        return renderable;
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
        _BasePath = renderWrap.BasePath;
    }

    public void UpdateGPUResource()
    {
        Writer.graphicsContext = renderWrap.graphicsContext;
        Writer.Clear();
        if (!resourcesInitialized)
            InitializeResources();

        Morph();
    }

    SkinningCompute skinningCompute = new SkinningCompute();
    void Morph()
    {
        RenderPipelineContext rpc = renderWrap.rpc;
        var renderers = rpc.renderers;

        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].WriteMatriticesData();
        }

        var graphicsContext = rpc.graphicsContext;
        meshPool.Reset();
        meshOverrides.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var model = renderer.model;
            var mesh = meshPool.Get(() => new Mesh());
            mesh.LoadIndex<int>(model.vertexCount, null);
            mesh.baseMesh = model.GetMesh();
            meshOverrides[renderer] = mesh;
            if (!renderer.skinning)
                continue;

            graphicsContext.UpdateMeshOneFrame<Vector3>(mesh, renderer.MeshPosition, MeshRenderable.POSITION);
            graphicsContext.CopyBaseMesh(mesh, MeshRenderable.NORMAL);
            graphicsContext.CopyBaseMesh(mesh, MeshRenderable.TANGENT);
            graphicsContext.EndUpdateMesh(mesh);
        }
        skinningCompute.context = this;
        Span<Matrix4x4> matrices = stackalloc Matrix4x4[1024];
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            if (!renderer.skinning)
                continue;
            var mesh = meshOverrides[renderer];

            int matrixCount = Math.Min(renderer.BoneMatricesData.Length, 1024);
            for (int j = 0; j < matrixCount; j++)
            {
                matrices[j] = Matrix4x4.Transpose(renderer.BoneMatricesData[j]);
            }
            if (matrixCount > 0)
                skinningCompute.Execute(mesh, MemoryMarshal.AsBytes(matrices.Slice(0, matrixCount)));
        }
    }

    public void SetMesh(Mesh mesh)
    {
        graphicsContext.SetMesh(mesh);
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

    public void Draw<T>(MeshRenderable<T> renderable)
    {
        renderWrap.graphicsContext.DrawIndexed(renderable.indexCount, renderable.indexStart, renderable.vertexStart);
    }

    public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
    {
        graphicsContext.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
    }

    public void Dispatch(int x = 1, int y = 1, int z = 1)
    {
        graphicsContext.Dispatch(x, y, z);
    }

    #region write object

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

    public static string _BasePath;

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
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }
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
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }

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
        if (fileName != null)
        {
            fileName = Path.Combine(_BasePath, fileName);
        }

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

    #region

    public void SetPSO(PSO pso, PSODesc desc)
    {
        var renderTargets = renderWrap.RenderTargets;
        if (pso.pixelShader != null && renderTargets.Count > 0)
            desc.rtvFormat = renderTargets[0].GetFormat();
        else
            desc.rtvFormat = Vortice.DXGI.Format.Unknown;
        desc.renderTargetCount = renderTargets.Count;
        graphicsContext.SetPSO(pso, desc);
    }

    public void SetPSO(ComputeShader computeShader)
    {
        graphicsContext.SetPSO(computeShader);
    }


    public void SetSRVs(params IGPUResource[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            graphicsContext.SetSRVTSlot(i, textures[i]);
        }
    }

    public void SetUAV(int slot, Texture2D texture2D)
    {
        graphicsContext.SetUAVTSlot(slot, texture2D);
    }

    public void SetUAV(int slot, GPUBuffer buffer)
    {
        graphicsContext.SetUAVTSlot(slot, buffer);
    }

    public void SetUAV(int slot, Texture2D texture2D, int mipIndex)
    {
        graphicsContext.SetUAVTSlot(slot, texture2D, mipIndex);
    }
    public void SetUAV(int slot, Mesh mesh, string bufferName)
    {
        graphicsContext.SetUAVTSlot(slot, mesh, bufferName);
    }

    public void SetSRV(int slot, Texture2D texture)
    {
        graphicsContext.SetSRVTSlot(slot, texture);
    }
    public void SetSRV<T>(int slot, T[] values) where T : unmanaged
    {
        SetSRV(slot, (ReadOnlySpan<T>)values);
    }
    public void SetSRV<T>(int slot, ReadOnlySpan<T> values) where T : unmanaged
    {
        graphicsContext.SetSRVTSlot(slot, values);
    }
    public void SetSRV(int slot, Mesh mesh, string bufferName)
    {
        graphicsContext.SetSRVTSlot(slot, mesh, bufferName);
    }
    public void SetSRV(int slot, GPUBuffer buffer)
    {
        graphicsContext.SetSRVTSlot(slot, buffer);
    }

    public void SetSRV(int slot, Texture2D texture, int mip)
    {
        graphicsContext.SetSRVTMip(slot, texture, mip);
    }

    public void SetCBV<T>(int slot, ReadOnlySpan<T> data) where T : unmanaged
    {
        graphicsContext.SetCBVRSlot<T>(slot, data);
    }

    public void SetCBV<T>(int slot, Span<T> data) where T : unmanaged
    {
        graphicsContext.SetCBVRSlot<T>(slot, data);
    }
    public void SetSimpleMesh(ReadOnlySpan<byte> vertexData, ReadOnlySpan<byte> indexData, int vertexStride, int indexStride)
    {
        graphicsContext.SetSimpleMesh(vertexData, indexData, vertexStride, indexStride);
    }
    public void SetSimpleMesh(ReadOnlySpan<byte> vertexData, ReadOnlySpan<ushort> indexData, int vertexStride)
    {
        graphicsContext.SetSimpleMesh(vertexData, MemoryMarshal.AsBytes(indexData), vertexStride, 2);
    }
    public void SetSimpleMesh(ReadOnlySpan<byte> vertexData, ReadOnlySpan<uint> indexData, int vertexStride)
    {
        graphicsContext.SetSimpleMesh(vertexData, MemoryMarshal.AsBytes(indexData), vertexStride, 4);
    }


    public void SetScissorRectAndViewport(int left, int top, int right, int bottom)
    {
        graphicsContext.RSSetScissorRectAndViewport(left, top, right, bottom);
    }

    #endregion
    public void Dispose()
    {
        meshOverrides.Clear();
        quadMesh?.Dispose();
        cubeMesh?.Dispose();
        skinningCompute.Dispose();
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
