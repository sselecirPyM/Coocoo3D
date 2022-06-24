using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class AnimationSystem
    {
        public Scene scene;

        public float playTime;

        public MainCaches caches;

        List<MMDRendererComponent> rendererComponents = new();
        public void Update()
        {
            foreach (var gameObject in scene.gameObjects)
            {
                var render = gameObject.GetComponent<MMDRendererComponent>();
                if (render != null)
                {
                    rendererComponents.Add(render);
                }
            }

            UpdateGameObjects((float)playTime, rendererComponents);
            rendererComponents.Clear();
        }

        void UpdateGameObjects(float playTime, IReadOnlyList<MMDRendererComponent> rendererComponents)
        {
            Parallel.For(0, rendererComponents.Count, i =>
            {
                var renderer = rendererComponents[i];
                renderer?.ComputeMotion(playTime, caches.GetMotion(renderer.motionPath));
            });
        }
    }
}
