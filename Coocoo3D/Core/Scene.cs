using Arch.Core;
using Arch.Core.Extensions;
using System;
using System.Collections.Generic;

namespace Coocoo3D.Core;

public class Scene
{
    public World world;

    public Coocoo3D.Core.Coocoo3DMain main;

    public event Action<Entity> OnObjectEnter;
    public event Action<Entity> OnObjectRemove;

    public Dictionary<Type, Action<Entity>> OnComponentAdd = new Dictionary<Type, Action<Entity>>();
    public Dictionary<Type, Action<Entity>> OnComponentRemove = new Dictionary<Type, Action<Entity>>();

    public void OnFrame()
    {

    }

    public void DuplicateObject(Entity obj)
    {
        main.OnRenderOnce.Enqueue(() =>
        {
            var newObj = world.Create();

            foreach (var component in obj.GetAllComponents())
            {
                var methodInfo = component.GetType().GetMethod("GetClone");
                object c = component;
                if (methodInfo != null)
                {
                    c = methodInfo.Invoke(component, null);
                }
                newObj.Add(c);
            }
        });
    }

    public Entity CreateEntity()
    {
        return world.Create();
    }

    public void SubscribeComponentAdded<T>(Arch.Core.Events.ComponentAddedHandler<T> action)
    {
        OnComponentAdd[typeof(T)] = (entity) => action(entity, ref entity.Get<T>());
    }

    public void SubscribeComponentRemoved<T>(Arch.Core.Events.ComponentAddedHandler<T> action)
    {
        OnComponentRemove[typeof(T)] = (entity) => action(entity, ref entity.Get<T>());
    }

    public void AddComponent<T>(Entity entity, T component)
    {
        entity.Add(component);
        if (OnComponentAdd.TryGetValue(typeof(T), out var action))
        {
            action(entity);
        }
    }

    public void DestroyEntity(Entity entity)
    {
        foreach (var component in entity.GetAllComponents())
        {
            if (OnComponentRemove.TryGetValue(component.GetType(), out var action))
            {
                action(entity);
            }
        }
        world.Destroy(entity);
    }
}
