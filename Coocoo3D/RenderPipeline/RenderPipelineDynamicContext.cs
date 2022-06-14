using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public int frameRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool CPUSkinning;

        public void Preprocess(IList<GameObject> gameObjects)
        {
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
        }

        public void FrameBegin()
        {
            renderers.Clear();
            meshRenderers.Clear();
            visuals.Clear();
            gameObjects.Clear();
        }
    }
}
