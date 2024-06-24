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
            animationState.Time = (float)gdc.PlayTime;
            animationState.ComputeMotion(caches.GetMotion(animationState.motionPath), renderer);
            renderer.ComputeMotion();
        });
        Parallel.For(0, animationRenderers.Count, i =>
        {
            var renderer = animationRenderers[i].Item1;
            renderer.ComputeVertexMorph(renderer.model.position);
        });
    }
}
