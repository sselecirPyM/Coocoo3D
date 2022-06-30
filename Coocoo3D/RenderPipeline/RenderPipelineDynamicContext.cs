using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class RenderPipelineDynamicContext
    {
        public List<MMDRendererComponent> renderers = new();
        public List<MeshRendererComponent> meshRenderers = new();
        public List<VisualComponent> visuals = new();

        public Dictionary<int, GameObject> gameObjects = new();

        public bool CPUSkinning;

        public void Preprocess(IReadOnlyList<GameObject> gameObjects)
        {
            FrameBegin();
            foreach (GameObject gameObject in gameObjects)
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
                    visuals.Add(visual);
                }
                this.gameObjects[gameObject.id] = gameObject;
            }
            foreach (var visual in visuals)
            {
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
        }

        void FrameBegin()
        {
            renderers.Clear();
            meshRenderers.Clear();
            visuals.Clear();
            gameObjects.Clear();
        }
    }
}
