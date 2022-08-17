using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class AnimationSession
    {
        public float progress;
        public float duration;
        public object target;
    }
    public class AnimationSystem
    {
        public Scene scene;

        public float playTime;

        public MainCaches caches;

        List<(MMDRendererComponent, AnimationStateComponent)> animationRenderers = new();
        public void Update()
        {
            animationRenderers.Clear();
            foreach (var gameObject in scene.gameObjects)
            {
                var render = gameObject.GetComponent<MMDRendererComponent>();
                var animation = gameObject.GetComponent<AnimationStateComponent>();
                if (render != null)
                {
                    animationRenderers.Add((render, animation));
                }
            }

            UpdateGameObjects((float)playTime, animationRenderers);
        }

        void UpdateGameObjects(float playTime, IReadOnlyList<(MMDRendererComponent, AnimationStateComponent)> animationRenderers)
        {
            Parallel.For(0, animationRenderers.Count, i =>
            {
                var renderer = animationRenderers[i].Item1;
                var animationState = animationRenderers[i].Item2;
                animationState.ComputeMotion(playTime, caches.GetMotion(animationState.motionPath), renderer.morphs, renderer.bones);
                for (int j = 0; j < renderer.morphs.Count; j++)
                {
                    if (renderer.morphs[j].Type == MorphType.Vertex && animationState.Weights.Computed[j] != renderer.weights[j])
                    {
                        renderer.meshNeedUpdate = true;
                        break;
                    }
                }
                animationState.Weights.Computed.CopyTo(renderer.weights, 0);
                renderer.ComputeMotion();
            });
            Parallel.For(0, animationRenderers.Count, i =>
            {
                var renderer = animationRenderers[i].Item1;
                renderer.ComputeVertexMorph(caches.GetModel(renderer.meshPath));
            });
        }
    }
}
