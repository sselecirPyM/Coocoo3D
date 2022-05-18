using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Coocoo3D.RenderPipeline
{
    public class RenderPipelineDynamicContext
    {
        public Settings settings;
        public List<MMDRendererComponent> renderers = new();
        public List<MeshRendererComponent> meshRenderers = new();
        public List<VolumeComponent> volumes = new();
        public List<ParticleEffectComponent> particleEffects = new();

        public List<DirectionalLightData> directionalLights = new();
        public List<PointLightData> pointLights = new();

        public Dictionary<MMDRendererComponent, int> findRenderer = new();
        public int frameRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool CPUSkinning;

        public void Preprocess(IList<GameObject> gameObjects)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject.TryGetComponent(out LightingComponent lighting))
                {
                    if (lighting.LightingType == LightingType.Directional)
                        directionalLights.Add(lighting.GetDirectionalLightData(gameObject.Transform.rotation));
                    if (lighting.LightingType == LightingType.Point)
                        pointLights.Add(lighting.GetPointLightData(gameObject.Transform.position));
                }
                if (gameObject.TryGetComponent(out VolumeComponent volume))
                {
                    volume.Position = gameObject.Transform.position;
                    volumes.Add(volume);
                }
                if (gameObject.TryGetComponent(out MMDRendererComponent renderer))
                {
                    renderers.Add(renderer);
                    findRenderer[renderer] = renderers.Count - 1;
                }
                if (gameObject.TryGetComponent(out MeshRendererComponent meshRenderer))
                {
                    meshRenderers.Add(meshRenderer);
                }
                if (gameObject.TryGetComponent(out ParticleEffectComponent particleEffect))
                {
                    particleEffects.Add(particleEffect);
                }
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].WriteMatriticesData();
            }
        }

        public void FrameBegin()
        {
            directionalLights.Clear();
            pointLights.Clear();
            volumes.Clear();
            renderers.Clear();
            findRenderer.Clear();
            particleEffects.Clear();
            meshRenderers.Clear();
        }
    }
}
