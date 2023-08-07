using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using DefaultEcs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coocoo3D.Core;

public class AnimationSystem
{
    public World world;
    public Scene scene;

    public float playTime;

    public MainCaches caches;

    EntitySet set;

    List<(MMDRendererComponent, AnimationStateComponent)> animationRenderers = new();

    public void Initialize()
    {
        set = world.GetEntities().With<MMDRendererComponent>().With<AnimationStateComponent>().AsSet();
    }

    public void Update()
    {
        animationRenderers.Clear();
        foreach (var gameObject in set.GetEntities())
        {
            var render = gameObject.Get<MMDRendererComponent>();
            var animation = gameObject.Get<AnimationStateComponent>();
            animationRenderers.Add((render, animation));
        }


        UpdateGameObjects(playTime);
    }

    void UpdateGameObjects(float playTime)
    {
        Parallel.For(0, animationRenderers.Count, i =>
        {
            var renderer = animationRenderers[i].Item1;
            var animationState = animationRenderers[i].Item2;
            animationState.ComputeMotion(playTime, caches.GetMotion(animationState.motionPath), renderer.Morphs, renderer.bones);
            for (int j = 0; j < renderer.Morphs.Count; j++)
            {
                if (renderer.Morphs[j].Type == MorphType.Vertex && animationState.Weights.Computed[j] != renderer.Weights[j])
                {
                    renderer.meshNeedUpdate = true;
                    break;
                }
            }
            animationState.Weights.Computed.CopyTo(renderer.Weights, 0);
            renderer.ComputeMotion();
        });
        Parallel.For(0, animationRenderers.Count, i =>
        {
            var renderer = animationRenderers[i].Item1;
            renderer.ComputeVertexMorph(renderer.model.position);
        });
    }
}
