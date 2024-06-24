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
    public abstract void Update();
}
public class SceneExtensionsSystem : IDisposable
{
    public EngineContext engineContext;

    public ConcurrentQueue<string> loadFiles = new ConcurrentQueue<string>();

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

    public void OpenFile(string path)
    {
        loadFiles.Enqueue(path);
    }

    public void ProcessFileLoad()
    {
        while(loadFiles.TryDequeue(out var file))
        {
            foreach(var loader in engineContext.extensionFactory.FileLoaders)
            {
                if (loader.Load(file))
                {
                    break;
                }
            }
        }
    }

    public void Update()
    {
        foreach (var sceneExtension in sceneExtensions)
        {
            sceneExtension.Update();
        }
    }

    List<ISceneExtension> sceneExtensions;

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