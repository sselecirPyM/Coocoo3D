using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Present;
using DefaultEcs;
using DefaultEcs.Command;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public World world;
        public EntityCommandRecorder recorder;

        public List<int> SelectedGameObjects = new();

        public int idAllocated = 1;

        public void OnFrame()
        {
            recorder.Execute();
            recorder.Clear();
        }

        public void DuplicateObject(Entity obj)
        {
            var world = recorder.Record(obj.World);
            var newObj = world.CreateEntity();

            if (TryGetComponent(obj, out VisualComponent visual))
                newObj.Set(visual.GetClone());
            if (TryGetComponent(obj, out MeshRendererComponent meshRenderer))
                newObj.Set(meshRenderer.GetClone());
            if (TryGetComponent(obj, out MMDRendererComponent mmdRenderer))
            {
                newObj.Set(obj.Get<Transform>());
                newObj.Set(mmdRenderer.GetClone());
            }
            if (TryGetComponent(obj, out AnimationStateComponent animationState))
            {
                newObj.Set(animationState.GetClone());
            }
            if (TryGetComponent(obj, out ObjectDescription description))
            {
                newObj.Set(description.GetClone());
            }
            newObj.Set(obj.Get<Transform>());
        }


        public void NewLighting()
        {
            var world = recorder.Record(this.world);
            var gameObject = world.CreateEntity();

            VisualComponent lightComponent = new VisualComponent();
            lightComponent.UIShowType = UIShowType.Light;
            gameObject.Set(lightComponent);
            gameObject.Set(new ObjectDescription
            {
                Name = "光照",
                Description = ""
            });
            gameObject.Set(new Transform(new Vector3(0, 1, 0), Quaternion.CreateFromYawPitchRoll(0, 1.3962634015954636615389526147909f, 0)));
        }

        public void NewDecal()
        {
            var world = recorder.Record(this.world);
            var gameObject = world.CreateEntity();

            VisualComponent decalComponent = new VisualComponent();
            decalComponent.UIShowType = UIShowType.Decal;
            gameObject.Set(decalComponent);
            gameObject.Set(new ObjectDescription
            {
                Name = "贴花",
                Description = ""
            });
            gameObject.Set(new Transform(new Vector3(0, 0, 0), Quaternion.CreateFromYawPitchRoll(0, -1.5707963267948966192313216916398f, 0), new Vector3(1, 1, 0.1f)));
        }

        static bool TryGetComponent<T>(Entity obj, out T value)
        {
            if (obj.Has<T>())
            {
                value = obj.Get<T>();
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }
    }
}
