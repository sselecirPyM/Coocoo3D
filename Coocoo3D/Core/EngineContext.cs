using System;
using System.Collections.Generic;

namespace Coocoo3D.Core;

public class EngineContext : IDisposable
{
    public List<object> systems = new();
    public Dictionary<Type, object> autoFills = new();

    public List<Action> syncCalls = new();

    public EngineContext()
    {
        autoFills.Add(typeof(EngineContext), this);
    }

    public void FillProperties(object o)
    {
        var type = o.GetType();
        var fields = type.GetFields();
        foreach (var field in fields)
        {
            if (autoFills.TryGetValue(field.FieldType, out var system1))
            {
                field.SetValue(o, system1);
            }
        }
    }

    public void InitializeObject(object o)
    {
        var type = o.GetType();
        var menthods = type.GetMethods();
        foreach (var method in menthods)
        {
            if (method.Name == "Initialize" && method.GetParameters().Length == 0)
            {
                method.Invoke(o, null);
                break;
            }
        }
    }

    public T AddSystem<T>() where T : class, new()
    {
        var system = new T();
        systems.Add(system);
        autoFills[typeof(T)] = system;
        return system;
    }

    public T AddSystem<T>(T system) where T : class
    {
        systems.Add(system);
        autoFills[typeof(T)] = system;
        return system;
    }

    public void InitializeSystems()
    {
        foreach (var system in systems)
        {
            FillProperties(system);
            InitializeObject(system);
        }
    }

    public void SyncCall(Action action)
    {
        syncCalls.Add(action);
    }

    public void SyncCallStage()
    {
        foreach (var action in syncCalls)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        syncCalls.Clear();
    }

    public void Dispose()
    {
        for (int i = systems.Count - 1; i >= 0; i--)
        {
            object system = systems[i];
            if (system is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
