using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using DefaultEcs.System;

namespace Coocoo3D.Core
{
    public class RenderSystem : ISystem<State>
    {
        public WindowSystem windowSystem;
        public GraphicsContext graphicsContext;
        public RenderPipelineContext renderPipelineContext;
        public MainCaches mainCaches;

        public List<Type> RenderPipelineTypes = new List<Type>();

        public void Initialize()
        {
            LoadRenderPipelines(new DirectoryInfo("Samples"));

            var rpc = renderPipelineContext;

            rpc.quadMesh.ReloadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
            mainCaches.MeshReadyToUpload.Enqueue(rpc.quadMesh);

            rpc.cubeMesh.ReloadIndex<int>(4, new int[]
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
            mainCaches.MeshReadyToUpload.Enqueue(rpc.cubeMesh);
        }

        List<VisualChannel> channels = new();
        public void Update(State state)
        {
            var context = renderPipelineContext;
            while (mainCaches.MeshReadyToUpload.TryDequeue(out var mesh))
                graphicsContext.UploadMesh(mesh);

            mainCaches.uploadHandler.graphicsContext = graphicsContext;
            mainCaches.uploadHandler.Update();
            mainCaches.uploadHandler.Output.Clear();

            context.CPUSkinning = false;

            foreach (var channel in windowSystem.visualChannels.Values)
            {
                if (channel.renderPipelineView != null)
                    channels.Add(channel);
                else
                {
                    channel.DelaySetRenderPipeline(RenderPipelineTypes[0]);
                    channels.Add(channel);
                }
            }
            foreach (var visualChannel in channels)
            {
                visualChannel.Onframe((float)context.Time);
                var renderPipeline = visualChannel.renderPipeline;
                var renderPipelineView = visualChannel.renderPipelineView;
                renderPipeline.renderWrap.rpc = context;

                foreach (var cap in renderPipelineView.sceneCaptures)
                {
                    var member = cap.Value.Item1;
                    var captureAttribute = cap.Value.Item2;
                    switch (captureAttribute.Capture)
                    {
                        case "Camera":
                            member.SetValue(renderPipeline, visualChannel.cameraData);
                            break;
                        case "Time":
                            member.SetValue(renderPipeline, context.Time);
                            break;
                        case "DeltaTime":
                            member.SetValue(renderPipeline, context.DeltaTime);
                            break;
                        case "RealDeltaTime":
                            member.SetValue(renderPipeline, context.RealDeltaTime);
                            break;
                        case "Recording":
                            member.SetValue(renderPipeline, context.recording);
                            break;
                        case "Visual":
                            member.SetValue(renderPipeline, context.visuals);
                            break;
                        case "Particle":
                            member.SetValue(renderPipeline, context.particles);
                            break;
                    }
                }
            }
            context.gpuWriter.graphicsContext = graphicsContext;
            context.gpuWriter.Clear();

            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                renderPipelineView.renderPipeline.BeforeRender();
            }
            UpdateGPUResource();
            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;
                renderPipelineView.PrepareRenderResources();
            }
            foreach (var visualChannel in channels)
            {
                var renderPipelineView = visualChannel.renderPipelineView;

                renderPipelineView.renderPipeline.Render();
                renderPipelineView.renderPipeline.AfterRender();
                renderPipelineView.renderWrap.AfterRender();
            }
            channels.Clear();
        }


        public void LoadRenderPipelines(DirectoryInfo dir)
        {
            RenderPipelineTypes.Clear();
            foreach (var file in dir.EnumerateFiles("*.dll"))
            {
                LoadRenderPipelineTypes(file.FullName);
            }
        }

        public void LoadRenderPipelineTypes(string path)
        {
            try
            {
                RenderPipelineTypes.AddRange(mainCaches.GetDerivedTypes(Path.GetFullPath(path), typeof(RenderPipeline.RenderPipeline)));
            }
            catch
            {

            }
        }

        LinearPool<Mesh> meshPool = new();
        public byte[] bigBuffer = new byte[0];

        public bool IsEnabled { get; set; } = true;

        void UpdateGPUResource()
        {
            var rpc = renderPipelineContext;
            var renderers = rpc.renderers;
            meshPool.Reset();
            rpc.meshOverride.Clear();
            #region Update bone data

            if (rpc.CPUSkinning)
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
                rpc.meshOverride[renderer] = mesh;
                if (!renderer.skinning) continue;

                if (rpc.CPUSkinning)
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
            rpc.findRenderer.Clear();
            while (rpc.CBs_Bone.Count < renderers.Count)
            {
                CBuffer constantBuffer = new CBuffer();
                constantBuffer.Mutable = true;
                rpc.CBs_Bone.Add(constantBuffer);
            }
            Span<Matrix4x4> mats = stackalloc Matrix4x4[1024];
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                var matrices = renderer.boneMatricesData;
                int l = Math.Min(matrices.Length, 1024);
                for (int k = 0; k < l; k++)
                    mats[k] = Matrix4x4.Transpose(matrices[k]);
                graphicsContext.UpdateResource<Matrix4x4>(rpc.CBs_Bone[i], mats.Slice(0, l));
                rpc.findRenderer[renderer] = i;
            }
            #endregion
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
            Span<Vector3> dat0 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
            //graphicsContext.BeginUpdateMesh(mesh);
            //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 0);
            mesh.AddBuffer(dat0.Slice(0, model.vertexCount), 0);//for compatibility

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
        }
    }
}