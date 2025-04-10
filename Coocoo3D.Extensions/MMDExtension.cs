using Arch.Core;
using Coocoo3D.Components;
using Coocoo3D.Core;
using System;
using System.ComponentModel.Composition;

namespace Coocoo3D.Extensions
{
    [Export(typeof(ISceneExtension))]
    public class MMDExtension : ISceneExtension, IDisposable
    {
        public EngineContext engineContext;
        public SceneExtensionsSystem sceneExtensions;
        public GameDriverContext gdc;

        PhysicsSystem physicsSystem = new PhysicsSystem();
        public override void Initialize()
        {
            engineContext.FillProperties(physicsSystem);
            physicsSystem.Initialize();

            sceneExtensions.AddSystem((Entity entity, ref MMDRendererComponent renderer, ref AnimationStateComponent animationState) =>
            {
                if (gdc.Playing)
                    animationState.Time += (float)gdc.DeltaTime;
                animationState.ComputeMotion(animationState.motion, renderer);
                renderer.ComputeMotion();

                renderer.ComputeVertexMorph();
            });
            sceneExtensions.AddCall(physicsSystem.Update);

        }

        public void Dispose()
        {
            physicsSystem.Dispose();
        }
    }
}
