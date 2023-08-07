using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.IO;

namespace Coocoo3D.Core;

public abstract class ISceneExtension
{
    public abstract void Initialize();
    public abstract void Update();
}
public class SceneExtensionsSystem : IDisposable
{
    public MainCaches mainCaches;
    public EngineContext engineContext;

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
            var extensions = mainCaches.GetDerivedTypes(Path.GetFullPath(path), typeof(ISceneExtension));
            foreach (var ext in extensions)
            {
                sceneExtensions.Add((ISceneExtension)Activator.CreateInstance(ext));
            }
        }
        catch (Exception e)
        {

        }
    }

    public void Dispose()
    {
        foreach (var sceneExtension in sceneExtensions)
            if (sceneExtension is IDisposable disposable)
                disposable.Dispose();
    }
}
