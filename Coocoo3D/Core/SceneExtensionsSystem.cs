using Arch.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;

namespace Coocoo3D.Core;

public abstract class ISceneExtension
{
    public abstract void Initialize();
}
public class SceneExtensionsSystem : IDisposable
{
    public EngineContext engineContext;

    public EditorContext editorContext;

    public ConcurrentQueue<string> loadFiles = new ConcurrentQueue<string>();

    public World world;

    List<ISceneExtension> sceneExtensions;
    List<Action> calls = new List<Action>();

    public void Initialize()
    {
        sceneExtensions = new List<ISceneExtension>();
        LoadExtDir(new DirectoryInfo("Extension"));

        foreach (var system in sceneExtensions)
        {
            engineContext.FillProperties(system);
            engineContext.InitializeObject(system);
        }
    }


    public void AddSystem<T>(ForEachWithEntity<T> call)
    {
        QueryDescription q = new QueryDescription().WithAll<T>();
        Action action = () =>
        {
            world.Query(in q, call);
        };
        calls.Add(action);
    }
    public void AddSystem<T0, T1>(ForEachWithEntity<T0, T1> call)
    {
        QueryDescription q = new QueryDescription().WithAll<T0, T1>();
        Action action = () =>
        {
            world.Query(in q, call);
        };
        calls.Add(action);
    }
    public void AddSystem<T0, T1, T2>(ForEachWithEntity<T0, T1, T2> call)
    {
        QueryDescription q = new QueryDescription().WithAll<T0, T1, T2>();
        Action action = () =>
        {
            world.Query(in q, call);
        };
        calls.Add(action);
    }

    public void AddCall(Action call)
    {
        calls.Add(call);
    }


    public void OpenFile(string path)
    {
        loadFiles.Enqueue(path);
    }

    public void ProcessFileLoad()
    {
        while (loadFiles.TryDequeue(out var file))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (editorContext.fileLoaders.TryGetValue(ext, out var callback))
            {
                callback(file);
            }
        }
    }

    public void Update()
    {
        foreach (var call in calls)
        {
            call();
        }
    }

    public void LoadExtDir(DirectoryInfo dir)
    {
        sceneExtensions.Clear();
        foreach (var file in dir.EnumerateFiles("*.dll"))
        {
            LoadExtensions(file.FullName);
        }
    }

    public void LoadExtensions(string path)
    {
        try
        {
            var extensions = GetInstances<ISceneExtension>(Path.GetFullPath(path));
            foreach (var ext in extensions)
            {
                sceneExtensions.Add((ISceneExtension)ext);
            }
        }
        catch (Exception e)
        {

        }
    }

    private List<T> GetInstances<T>(string path)
    {
        var cat = new AssemblyCatalog(Assembly.LoadFrom(path));
        CompositionContainer container = new CompositionContainer(cat);

        var exports = container.GetExportedValues<T>();
        List<T> instances = new List<T>(exports);
        return instances;
    }

    public void Dispose()
    {
        foreach (var sceneExtension in sceneExtensions)
            if (sceneExtension is IDisposable disposable)
                disposable.Dispose();
    }
}