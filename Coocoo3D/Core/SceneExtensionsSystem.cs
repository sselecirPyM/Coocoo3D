using Coocoo3D.RenderPipeline;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public abstract class ISceneExtension
    {
        public abstract void Initialize();
        public abstract void Update();
    }
    public class SceneExtensionsSystem : IDisposable
    {
        public MainCaches mainCaches;
        public GameDriverContext gameDriverContext;
        public World world;

        public Dictionary<Type, object> systems1 = new();
        T AddSystem<T>(T system) where T : class
        {
            //systems.Add(system);
            systems1[typeof(T)] = system;
            return system;
        }
        public void Initialize()
        {
            sceneExtensions = new List<ISceneExtension>();
            LoadExtDir(new DirectoryInfo("Extension"));

            AddSystem(gameDriverContext);
            AddSystem(mainCaches);
            AddSystem(world);
            foreach (var system in sceneExtensions)
            {
                var type = system.GetType();
                var fields = type.GetFields();
                foreach (var field in fields)
                {
                    if (systems1.TryGetValue(field.FieldType, out var system1))
                    {
                        field.SetValue(system, system1);
                    }
                }
                var menthods = type.GetMethods();
                foreach (var method in menthods)
                {
                    if (method.Name == "Initialize" && method.GetParameters().Length == 0)
                    {
                        method.Invoke(system, null);
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
                LoadExts(file.FullName);
            }
        }

        public void LoadExts(string path)
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
}
