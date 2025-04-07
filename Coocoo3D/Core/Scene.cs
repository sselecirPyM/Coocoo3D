using Coocoo3D.Components;
using Coocoo3D.Present;
using DefaultEcs;
using DefaultEcs.Command;
using System;
using System.Collections.Generic;

namespace Coocoo3D.Core;

public class Scene
{
    public World world;
    public EntityCommandRecorder recorder;

    public Dictionary<int, Entity> gameObjects = new Dictionary<int, Entity>();

    public int idAllocated = 1;

    public event Action<Entity> OnObjectEnter;
    public event Action<Entity> OnObjectRemove;

    public void Initialize()
    {
        world.SubscribeComponentAdded<VisualComponent>(OnAdd);
    }

    public void OnFrame()
    {
        recorder.Execute();
        recorder.Clear();

        gameObjects.Clear();


        foreach (Entity gameObject in world)
        {
            this.gameObjects[gameObject.GetHashCode()] = gameObject;
        }
    }

    public void OnAdd(in Entity entity, in VisualComponent visual)
    {
        visual.id = GetNewId();
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

    int GetNewId()
    {
        idAllocated++;
        return idAllocated - 1;
    }
}
