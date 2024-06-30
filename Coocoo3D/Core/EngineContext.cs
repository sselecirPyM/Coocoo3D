using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;

namespace Coocoo3D.Core;

public class EngineContext : IDisposable
{
    public List<object> systems = new();
    public Dictionary<Type, object> autoFills = new();

    public ConcurrentQueue<Action> frameBeginCalls = new();
    public ConcurrentQueue<Action> frameEndCalls = new();

    public ExtensionFactory extensionFactory;

    public static CompositionContainer compositionContainer;

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
        }
        foreach (var system in systems)
        {
            InitializeObject(system);
        }
        var cat = new TypeCatalog(typeof(ExtensionFactory));
        var cat2 = new DirectoryCatalog("Extension");
        compositionContainer = new CompositionContainer(new AggregateCatalog(cat, cat2));
        extensionFactory = compositionContainer.GetExportedValue<ExtensionFactory>();
        foreach (var o in extensionFactory.EditorAccess)
        {
            FillProperties(o);
        }
        foreach (var o in extensionFactory.EditorAccess)
        {
            o.Initialize();
        }
    }

    public void BeforeFrameBegin(Action action)
    {
        frameBeginCalls.Enqueue(action);
    }

    public void FrameEnd(Action action)
    {
        frameBeginCalls.Enqueue(action);
    }

    public void _OnFrameBegin()
    {
        while (frameBeginCalls.TryDequeue(out var action))
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
    }

    public void _OnFrameEnd()
    {
        while (frameEndCalls.TryDequeue(out var action))
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
        foreach (var editorAccess in extensionFactory.EditorAccess)
        {
            if (editorAccess is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
