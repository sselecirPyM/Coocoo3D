using Caprice.Attributes;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
                graphicsContext.SetCBVRSlot(GetBoneBuffer(renderer), 0);
            }
            for (int i = 0; i < renderer.Materials.Count; i++)
            {
                var material = renderer.Materials[i];
                var submesh = model.Submeshes[i];
                var renderable = new MeshRenderable()
                {
                    mesh = meshOverride?? mesh,
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
        return GetIndexableValue(key, material.Parameters);
    }

    public object GetIndexableValue(string key, IDictionary<string, object> material)
    {
        if (material != null && material.TryGetValue(key, out object obj1)
            && paramBaseType.TryGetValue(key, out var type))
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

    public GPUWriter Writer { get => renderWrap.rpc.gpuWriter; }


    public void PushParameters(object parameterProvider)
    {
        var type = parameterProvider.GetType();

        if (memberInfoCache.TryGetValue(type, out var parameters))
        {

        }
        else
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

    public void Write(IReadOnlyList<object> datas, GPUWriter writer, IDictionary<string, object> material = null)
    {
        foreach (var obj in datas)
            WriteObject(obj, writer, material);
    }

    public void Write(IReadOnlyList<object> datas, GPUWriter writer, RenderMaterial material = null)
    {
        foreach (var obj in datas)
            WriteObject(obj, writer, material.Parameters);
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
    }
}
