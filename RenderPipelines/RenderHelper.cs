using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
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

        public IEnumerable<MeshRenderable> MeshRenderables(bool setMesh = true)
        {
            RenderPipelineContext rpc = renderWrap.rpc;
            var graphicsContext = rpc.graphicsContext;
            foreach (var renderer in rpc.renderers)
            {
                var model = GetModel(renderer.meshPath);
                var mesh = model.GetMesh();
                var meshOverride = meshOverrides[renderer];
                if (setMesh)
                {
                    graphicsContext.SetMesh(mesh, meshOverride);
                    graphicsContext.SetCBVRSlot(GetBoneBuffer(renderer), 0);
                }
                for (int i = 0; i < renderer.Materials.Count; i++)
                {
                    var material = renderer.Materials[i];
                    var submesh = model.Submeshes[i];
                    var renderable = new MeshRenderable()
                    {
                        mesh = mesh,
                        meshOverride = meshOverride,
                        transform = renderer.LocalToWorld,
                        gpuSkinning = renderer.skinning && !CPUSkinning,
                    };
                    renderable.material = material;
                    WriteRenderable1(ref renderable, submesh);
                    yield return renderable;
                }
            }
            foreach (var renderer in rpc.meshRenderers)
            {
                var model = GetModel(renderer.meshPath);
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
                        meshOverride = null,
                        transform = renderer.transform.GetMatrix(),
                        gpuSkinning = false,
                    };
                    renderable.material = material;
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
            quadMesh.ReloadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
            cubeMesh.ReloadIndex<int>(4, new int[]
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
            var graphicsContext = renderWrap.rpc.graphicsContext;
            graphicsContext.UploadMesh(quadMesh);
            graphicsContext.UploadMesh(cubeMesh);
        }

        public void UpdateGPUResource()
        {
            if(!resourcesInitialized)
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
                var matrices = renderer.boneMatricesData;
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
                    bufferSize = Math.Max(GetModel(renderer.meshPath).vertexCount, bufferSize);
            }
            bufferSize *= 12;
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
                var model = GetModel(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
                meshOverrides[renderer] = mesh;
                if (!renderer.skinning)
                    continue;

                graphicsContext.UpdateMeshOneFrame<Vector3>(mesh, renderer.meshPositionCache, 0);
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
                var model = GetModel(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
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
                var model = GetModel(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
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
            Parallel.ForEach(rangePartitioner, (range, loopState) =>
            {
                Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                int from = range.Item1;
                int to = range.Item2;
                for (int j = from; j < to; j++)
                {
                    Vector3 pos0 = renderer.meshPositionCache[j];
                    Vector3 pos1 = Vector3.Zero;
                    int a = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int boneId = model.boneId[j * 4 + k];
                        if (boneId >= renderer.bones.Count)
                            break;
                        Matrix4x4 trans = renderer.boneMatricesData[boneId];
                        float weight = model.boneWeights[j * 4 + k];
                        pos1 += Vector3.Transform(pos0, trans) * weight;
                        a++;
                    }
                    if (a > 0)
                        _d3[j] = pos1;
                    else
                        _d3[j] = pos0;
                }
            });
            Span<Vector3> dat0 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
            //graphicsContext.BeginUpdateMesh(mesh);
            //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 0);
            mesh.AddBuffer<Vector3>(dat0.Slice(0, model.vertexCount), 0);//for compatibility

            Parallel.ForEach(rangePartitioner, (range, loopState) =>
            {
                Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                int from = range.Item1;
                int to = range.Item2;
                for (int j = from; j < to; j++)
                {
                    Vector3 norm0 = model.normal[j];
                    Vector3 norm1 = Vector3.Zero;
                    int a = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int boneId = model.boneId[j * 4 + k];
                        if (boneId >= renderer.bones.Count) break;
                        Matrix4x4 trans = renderer.boneMatricesData[boneId];
                        float weight = model.boneWeights[j * 4 + k];
                        norm1 += Vector3.TransformNormal(norm0, trans) * weight;
                        a++;
                    }
                    if (a > 0)
                        _d3[j] = Vector3.Normalize(norm1);
                    else
                        _d3[j] = Vector3.Normalize(norm0);
                }
            });

            //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 1);
            mesh.AddBuffer<Vector3>(dat0.Slice(0, model.vertexCount), 1);//for compatibility

            //graphicsContext.EndUpdateMesh(mesh);
        }

        ModelPack GetModel(string path) => renderWrap.rpc.mainCaches.GetModel(path);

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
}
