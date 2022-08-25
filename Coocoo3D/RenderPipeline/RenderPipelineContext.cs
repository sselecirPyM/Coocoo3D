using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Present;

namespace Coocoo3D.RenderPipeline
{
    public class RenderPipelineContext : IDisposable
    {
        public Scene scene;
        public ParticleSystem particleSystem;
        public MainCaches mainCaches;

        public Mesh quadMesh = new Mesh();
        public Mesh cubeMesh = new Mesh();

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext = new GraphicsContext();

        public Dictionary<MMDRendererComponent, int> findRenderer = new();

        public List<CBuffer> CBs_Bone = new();

        internal Wrap.GPUWriter gpuWriter = new();

        public bool recording = false;

        public bool CPUSkinning = false;

        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        #region Collect data

        public List<MMDRendererComponent> renderers = new();
        public List<MeshRendererComponent> meshRenderers = new();
        public List<GameObject> visuals = new();
        public List<(RenderMaterial, ParticleHolder)> particles = new();

        public Dictionary<int, GameObject> gameObjects = new();

        public void FrameBegin()
        {
            renderers.Clear();
            meshRenderers.Clear();
            visuals.Clear();
            gameObjects.Clear();
            particles.Clear();
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
            foreach (var particle in particleSystem.particles)
            {
                var gameObject = gameObjects[particle.Key];
                var visual = gameObject.GetComponent<VisualComponent>();
                particle.Value.transform = gameObject.Transform;
                particles.Add((visual.material, particle.Value));
            }
        }

        #endregion
        public void Initialize()
        {
            graphicsContext.Reload(graphicsDevice);
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[findRenderer[rendererComponent]];
        }

        public Dictionary<MMDRendererComponent, Mesh> meshOverride = new();

        public void Dispose()
        {
            cubeMesh.Dispose();
            quadMesh.Dispose();
        }
    }
}
