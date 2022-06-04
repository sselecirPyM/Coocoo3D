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

        //public List<DirectionalLightData> directionalLights = new();
        //public List<PointLightData> pointLights = new();
        public List<VisualComponent> visuals = new();

        public int frameRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool CPUSkinning;

        public void Preprocess(IList<GameObject> gameObjects)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                //if (gameObject.TryGetComponent(out LightingComponent lighting))
                //{
                //    if (lighting.LightingType == LightingType.Directional)
                //        directionalLights.Add(lighting.GetDirectionalLightData(gameObject.Transform.rotation));
                //    if (lighting.LightingType == LightingType.Point)
                //        pointLights.Add(lighting.GetPointLightData(gameObject.Transform.position));
                //}
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
            }
        }

        public void FrameBegin()
        {
            //directionalLights.Clear();
            //pointLights.Clear();
            renderers.Clear();
            meshRenderers.Clear();
            visuals.Clear();
        }
    }
}
