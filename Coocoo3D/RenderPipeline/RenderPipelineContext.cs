using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Coocoo3D.Present;

namespace Coocoo3D.RenderPipeline
{
    public class RenderPipelineContext : IDisposable
    {
        public Scene scene;
        public MainCaches mainCaches;

        public Mesh quadMesh = new Mesh();
        public Mesh cubeMesh = new Mesh();

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext = new GraphicsContext();

        public Dictionary<MMDRendererComponent, int> findRenderer = new();

        public List<CBuffer> CBs_Bone = new();

        internal Wrap.GPUWriter gpuWriter = new Wrap.GPUWriter();

        public bool recording = false;

        public bool CPUSkinning1 = false;


        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        #region X

        public List<MMDRendererComponent> renderers = new();
        public List<MeshRendererComponent> meshRenderers = new();
        public List<GameObject> visuals = new();
        public List<(RenderMaterial, ParticleHolder)> particles = new();

        public Dictionary<int, GameObject> gameObjects = new();

        public bool CPUSkinning;
        void Preprocess()
        {
            foreach (GameObject gameObject in scene.gameObjects)
            {
                if (gameObject.TryGetComponent(out MMDRendererComponent renderer))
                {
                    renderers.Add(renderer);
                }
                if (gameObject.TryGetComponent(out MeshRendererComponent meshRenderer))
                {
                    meshRenderers.Add(meshRenderer);
                }
                if (gameObject.TryGetComponent(out VisualComponent visual))
                {
                    visual.transform = gameObject.Transform;
                    visuals.Add(gameObject);
                }
                this.gameObjects[gameObject.id] = gameObject;
            }
            foreach (var visualObject in visuals)
            {
                var visual = visualObject.GetComponent<VisualComponent>();
                if (visual.bindBone != null && this.gameObjects.TryGetValue(visual.bindId, out var gameObject) &&
                    gameObject.TryGetComponent<MMDRendererComponent>(out var renderer))
                {
                    var bone = renderer.bones.Find(u => u.Name == visual.bindBone);
                    if (bone == null)
                        continue;
                    Vector3 pos1 = visual.transform.position;
                    bone.GetPosRot(out var pos, out var rot);
                    Vector3 pos2 = new Vector3(visual.bindX ? 0 : pos1.X, visual.bindY ? 0 : pos1.Y, visual.bindZ ? 0 : pos1.Z);

                    Vector3 position = pos + Vector3.Transform(pos1, rot);

                    position = Vector3.Transform(position, renderer.LocalToWorld);
                    if (!visual.bindX)
                        position.X = pos1.X;
                    if (!visual.bindY)
                        position.Y = pos1.Y;
                    if (!visual.bindZ)
                        position.Z = pos1.Z;
                    visual.transform = new Transform(position, visual.transform.rotation * (visual.bindRot ? rot : Quaternion.Identity), visual.transform.scale);
                }
            }
            particles.Clear();
            foreach (var particle in scene.particles)
            {
                var gameObject = gameObjects[particle.Key];
                var visual = gameObject.GetComponent<VisualComponent>();
                particle.Value.transform = gameObject.Transform;
                particles.Add((visual.material, particle.Value));
            }
        }

        void FrameBegin()
        {
            renderers.Clear();
            meshRenderers.Clear();
            visuals.Clear();
            gameObjects.Clear();
        }
        #endregion
        public void Initialize()
        {
            graphicsContext.Reload(graphicsDevice);

            quadMesh.ReloadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
            mainCaches.MeshReadyToUpload.Enqueue(quadMesh);

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
            mainCaches.MeshReadyToUpload.Enqueue(cubeMesh);
        }

        public void Submit()
        {
            CPUSkinning = CPUSkinning1;

            FrameBegin();
            Preprocess();
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[findRenderer[rendererComponent]];
        }

        LinearPool<Mesh> meshPool = new();
        public Dictionary<MMDRendererComponent, Mesh> meshOverride = new();
        public byte[] bigBuffer = new byte[0];
        public void UpdateGPUResource()
        {
            meshPool.Reset();
            meshOverride.Clear();
            #region Update bone data

            if (CPUSkinning1)
            {
                int bufferSize = 0;
                foreach (var renderer in renderers)
                {
                    if (renderer.skinning)
                        bufferSize = Math.Max(GetModelPack(renderer.meshPath).vertexCount, bufferSize);
                }
                bufferSize *= 12;
                if (bufferSize > bigBuffer.Length)
                    bigBuffer = new byte[bufferSize];
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                var model = GetModelPack(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
                meshOverride[renderer] = mesh;
                if (!renderer.skinning) continue;

                if (CPUSkinning1)
                {
                    Skinning(model, renderer, mesh);
                }
                else
                {
                    if (renderer.meshNeedUpdate)
                    {
                        graphicsContext.BeginUpdateMesh(mesh);
                        graphicsContext.UpdateMesh<Vector3>(mesh, renderer.meshPositionCache, 0);
                        graphicsContext.EndUpdateMesh(mesh);
                    }
                }
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].WriteMatriticesData();
            }
            findRenderer.Clear();
            while (CBs_Bone.Count < renderers.Count)
            {
                CBuffer constantBuffer = new CBuffer();
                constantBuffer.Mutable = true;
                CBs_Bone.Add(constantBuffer);
            }
            Span<Matrix4x4> mats = stackalloc Matrix4x4[1024];
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                var matrices = renderer.boneMatricesData;
                int l = Math.Min(matrices.Length, 1024);
                for (int k = 0; k < l; k++)
                    mats[k] = Matrix4x4.Transpose(matrices[k]);
                graphicsContext.UpdateResource(CBs_Bone[i], mats.Slice(0, l));
                findRenderer[renderer] = i;
            }
            #endregion
        }
        public void Skinning(ModelPack model, MMDRendererComponent renderer, Mesh mesh)
        {
            const int parallelSize = 1024;
            Span<Vector3> dat0 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
            Parallel.For(0, (model.vertexCount + parallelSize - 1) / parallelSize, u =>
            {
                Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                int from = u * parallelSize;
                int to = Math.Min(from + parallelSize, model.vertexCount);
                for (int j = from; j < to; j++)
                {
                    Vector3 pos0 = renderer.meshPositionCache[j];
                    Vector3 pos1 = Vector3.Zero;
                    int a = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int boneId = model.boneId[j * 4 + k];
                        if (boneId >= renderer.bones.Count) break;
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
            //graphicsContext.BeginUpdateMesh(mesh);
            //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 0);
            mesh.AddBuffer(dat0.Slice(0, model.vertexCount), 0);//for compatibility

            Parallel.For(0, (model.vertexCount + parallelSize - 1) / parallelSize, u =>
            {
                Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                int from = u * parallelSize;
                int to = Math.Min(from + parallelSize, model.vertexCount);
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
            mesh.AddBuffer(dat0.Slice(0, model.vertexCount), 1);//for compatibility

            //graphicsContext.EndUpdateMesh(mesh);
            graphicsContext.UploadMesh(mesh);//for compatibility
        }

        ModelPack GetModelPack(string path) => mainCaches.GetModel(path);

        public void Dispose()
        {
            foreach (var obj in meshPool.list1)
            {
                obj.Dispose();
            }
            cubeMesh.Dispose();
            quadMesh.Dispose();
        }
    }
}
