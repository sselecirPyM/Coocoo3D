using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using DefaultEcs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coocoo3D.Core;

public class AnimationSystem
{
    public World world;

    public MainCaches caches;
    public GameDriverContext gdc;

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


        Parallel.For(0, animationRenderers.Count, i =>
        {
            var renderer = animationRenderers[i].Item1;
            var animationState = animationRenderers[i].Item2;
            //animationState.Time = playTime;
            if (gdc.Playing)
                animationState.Time += (float)gdc.DeltaTime;
            animationState.ComputeMotion(animationState.motion, renderer);
            renderer.ComputeMotion();

            renderer.ComputeVertexMorph(renderer.model.position);
        });
    }
}
