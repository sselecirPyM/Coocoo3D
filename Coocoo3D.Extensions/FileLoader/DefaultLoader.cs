using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Extensions.FileFormat;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Coocoo3D.Extensions.FileLoader;
[Export(typeof(IEditorAccess))]
public class DefaultLoader : IEditorAccess
{
    public MainCaches mainCaches;
    public Scene scene;
    public EditorContext editorContext;
    public GameDriver gameDriver;

    public void Initialize()
    {
        editorContext.RegisterFileLoader(".pmx", (path) =>
        {
            mainCaches.ProxyCall(() =>
            {
                ModelPack modelPack = mainCaches.GetResource<ModelPack>(path);
                if (modelPack == null)
                    return;


                modelPack.LoadPmx(scene);
            });
        });
        Action<string> loadGlTF = (path) =>
        {
            mainCaches.ProxyCall(() =>
            {
                ModelPack modelPack = mainCaches.GetResource<ModelPack>(path);
                if (modelPack == null)
                    return;


                var entity = scene.CreateEntity();
                modelPack.LoadMesh(entity);
            });
        };
        editorContext.RegisterFileLoader(".gltf", loadGlTF);
        editorContext.RegisterFileLoader(".glb", loadGlTF);
        editorContext.RegisterFileLoader(".vrm", loadGlTF);

        editorContext.RegisterFileLoader(".vmd", OpenVMDFile);
        editorContext.RegisterFileLoader(".coocoo3dscene", LoadScene);
    }

    void OpenVMDFile(string path)
    {
        using var stream = File.OpenRead(path);
        using BinaryReader reader = new BinaryReader(stream);
        VMDFormat motionSet = VMDFormat.Load(reader);
        if (motionSet.CameraKeyFrames.Count != 0)
        {
            var camera = editorContext.currentChannel.camera;
            var cameraKeyFrames = new List<CameraKeyFrame>();
            cameraKeyFrames.AddRange(motionSet.CameraKeyFrames);
            camera.cameraMotion.cameraKeyFrames = cameraKeyFrames;
            for (int i = 0; i < cameraKeyFrames.Count; i++)
            {
                CameraKeyFrame frame = cameraKeyFrames[i];
                frame.distance *= 0.1f;
                frame.position *= 0.1f;
                cameraKeyFrames[i] = frame;
            }
            camera.CameraMotionOn = true;
        }
        else
        {
            if (editorContext.selectedObject.IsAlive() && TryGetComponent(editorContext.selectedObject, out Components.AnimationStateComponent animationState))
            {
                animationState.motion = mainCaches.GetResource<MMDMotion>(path);
            }
        }
        gameDriver.RequireRender(true);
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

    void LoadScene(string path)
    {
        var scene1 = ReadJsonStream<Coocoo3D.Extensions.FileFormat.Coocoo3DScene>(File.OpenRead(path));
        scene1.ToScene(scene, mainCaches);
    }

    static T ReadJsonStream<T>(Stream stream)
    {
        JsonSerializer jsonSerializer = new JsonSerializer();
        jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
        using StreamReader reader1 = new StreamReader(stream);
        return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
    }
}
