using Coocoo3D.Common;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Numerics;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;
using System.Runtime.InteropServices;

namespace Coocoo3D.RenderPipeline
{
    public class RecordSettings
    {
        public float FPS;
        public float StartTime;
        public float StopTime;
        public int Width;
        public int Height;
    }
    public class GameDriverContext
    {
        public int NeedRender;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public float FrameInterval;
        public float PlaySpeed;
        public bool RequireResetPhysics;
        public TimeManager timeManager;

        public void RequireRender(bool updateEntities)
        {
            if (updateEntities)
                RequireResetPhysics = true;
            NeedRender = 10;
        }
    }

    public class RenderPipelineContext : IDisposable
    {
        public RecordSettings recordSettings = new RecordSettings()
        {
            FPS = 60,
            Width = 1920,
            Height = 1080,
            StartTime = 0,
            StopTime = 9999,
        };

        public MainCaches mainCaches = new();

        public Dictionary<string, VisualChannel> visualChannels = new();

        public VisualChannel currentChannel;

        public Mesh quadMesh = new Mesh();
        public int frameRenderCount;

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext = new GraphicsContext();
        public SwapChain swapChain = new SwapChain();

        public RenderPipelineDynamicContext dynamicContextRead = new();
        private RenderPipelineDynamicContext dynamicContextWrite = new();

        public List<CBuffer> CBs_Bone = new();

        public Format outputFormat = Format.R8G8B8A8_UNorm;
        public Format swapChainFormat { get => swapChain.format; }

        public Recorder recorder;

        public string currentPassSetting = "Samples\\samplePasses.coocoox";

        internal Wrap.GPUWriter gpuWriter = new Wrap.GPUWriter();

        public GameDriverContext gameDriverContext = new GameDriverContext()
        {
            FrameInterval = 1 / 240.0f,
        };

        public Type[] RenderPipelineTypes;

        public string rpBasePth;

        public bool recording = false;

        public bool CPUSkinning = false;

        public void Load()
        {
            graphicsDevice = new GraphicsDevice();
            graphicsContext.Reload(graphicsDevice);

            quadMesh.ReloadIndex<int>(4, new int[] { 0, 1, 2, 2, 1, 3 });
            mainCaches.MeshReadyToUpload.Enqueue(quadMesh);
            currentPassSetting = Path.GetFullPath(currentPassSetting);
            recorder = new Recorder()
            {
                graphicsDevice = graphicsDevice,
                graphicsContext = graphicsContext,
            };
            rpBasePth = Path.GetFullPath("Samples");
            RenderPipelineTypes = mainCaches.GetTypes(Path.GetFullPath("RenderPipelines.dll"), typeof(RenderPipeline));
            currentChannel = AddVisualChannel("main");
        }

        public RenderPipelineDynamicContext GetDynamicContext(Scene scene)
        {
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.settings = scene.settings.GetClone();

            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            dynamicContextWrite.CPUSkinning = CPUSkinning;

            dynamicContextWrite.Time = gameDriverContext.PlayTime;
            dynamicContextWrite.DeltaTime = gameDriverContext.Playing ? gameDriverContext.DeltaTime : 0;

            dynamicContextWrite.Preprocess(scene.gameObjects);

            frameRenderCount++;
            return dynamicContextWrite;
        }

        public void Submit(RenderPipelineDynamicContext dynamicContext)
        {
            dynamicContextWrite = dynamicContextRead;
            dynamicContextRead = dynamicContext;
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[dynamicContextRead.findRenderer[rendererComponent]];
        }

        LinearPool<Mesh> meshPool = new();
        public Dictionary<MMDRendererComponent, Mesh> meshOverride = new();
        public byte[] bigBuffer = new byte[0];
        public void UpdateGPUResource()
        {
            meshPool.Reset();
            meshOverride.Clear();
            #region Update bone data
            var renderers = dynamicContextRead.renderers;
            while (CBs_Bone.Count < renderers.Count)
            {
                CBuffer constantBuffer = new CBuffer();
                constantBuffer.Mutable = true;
                CBs_Bone.Add(constantBuffer);
            }

            if (CPUSkinning)
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
            Parallel.ForEach(renderers, renderer =>
            {
                renderer.ComputeVertexMorph();
            });
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                var model = GetModelPack(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
                meshOverride[renderer] = mesh;
                if (!renderer.skinning) continue;

                if (CPUSkinning)
                {
                    Skinning(model, renderer, mesh);
                }
                else
                {
                    if (renderer.meshNeedUpdate)
                    {
                        graphicsContext.BeginUpdateMesh(mesh);
                        graphicsContext.UpdateMesh<Vector3>(mesh, renderer.meshPosData1, 0);
                        graphicsContext.EndUpdateMesh(mesh);
                    }
                }
                var matrices = renderer.boneMatricesData;
                for (int k = 0; k < matrices.Length; k++)
                    matrices[k] = Matrix4x4.Transpose(matrices[k]);
                graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], matrices);
            }
            #endregion
        }
        public void Skinning(ModelPack model, MMDRendererComponent renderer, Mesh mesh)
        {
            const int parallelSize = 1024;
            Span<Vector3> d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
            Parallel.For(0, (model.vertexCount + parallelSize - 1) / parallelSize, u =>
            {
                Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                int from = u * parallelSize;
                int to = Math.Min(from + parallelSize, model.vertexCount);
                for (int j = from; j < to; j++)
                {
                    Vector3 pos0 = renderer.meshPosData1[j];
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
            mesh.AddBuffer(d3.Slice(0, model.vertexCount), 0);//for compatibility

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
            mesh.AddBuffer(d3.Slice(0, model.vertexCount), 1);//for compatibility

            //graphicsContext.EndUpdateMesh(mesh);
            graphicsContext.UploadMesh(mesh);//for compatibility
        }

        Queue<string> delayAddVisualChannel = new();
        Queue<string> delayRemoveVisualChannel = new();
        public void DelayAddVisualChannel(string name)
        {
            delayAddVisualChannel.Enqueue(name);
        }
        public void DelayRemoveVisualChannel(string name)
        {
            delayRemoveVisualChannel.Enqueue(name);
        }

        public VisualChannel AddVisualChannel(string name)
        {
            var visualChannel = new VisualChannel();
            visualChannels[name] = visualChannel;
            visualChannel.Name = name;
            visualChannel.graphicsContext = graphicsContext;

            visualChannel.DelaySetRenderPipeline(RenderPipelineTypes[0], this, rpBasePth);

            return visualChannel;
        }

        public void RemoveVisualChannel(string name)
        {
            if (visualChannels.Remove(name, out var vc))
            {
                if (vc == currentChannel)
                    currentChannel = visualChannels["main"];
                vc.Dispose();
            }
        }

        public void PreConfig()
        {
            while (delayAddVisualChannel.TryDequeue(out var vcName))
                AddVisualChannel(vcName);
            while (delayRemoveVisualChannel.TryDequeue(out var vcName))
                RemoveVisualChannel(vcName);
        }

        public ModelPack GetModelPack(string path) => mainCaches.GetModel(path);

        public void AfterRender()
        {
            recorder.OnFrame();
        }

        public void Dispose()
        {

        }
    }
}
