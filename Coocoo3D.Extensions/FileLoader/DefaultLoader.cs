using Coocoo3D.Core;
using Coocoo3D.Extensions.FileFormat;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using DefaultEcs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Coocoo3D.Extensions.FileLoader;
[Export(typeof(IFileLoader))]
[Export(typeof(IEditorAccess))]
public class DefaultLoader : IFileLoader, IEditorAccess
{
    public MainCaches mainCaches;
    public Scene scene;
    public EditorContext editorContext;
    public GameDriver gameDriver;

    public bool Load(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".pmx":
                mainCaches.ProxyCall(() =>
                {
                    ModelPack modelPack = mainCaches.GetModel(path);
                    if (modelPack == null)
                        return;

                    var world = scene.recorder.Record(scene.world);

                    var entity = world.CreateEntity();
                    modelPack.LoadPmx(entity);
                });
                break;
            case ".gltf":
            case ".glb":
            case ".vrm":
                mainCaches.ProxyCall(() =>
                {
                    ModelPack modelPack = mainCaches.GetModel(path);
                    if (modelPack == null)
                        return;

                    var world = scene.recorder.Record(scene.world);

                    var entity = world.CreateEntity();
                    modelPack.LoadMesh(entity);
                });
                break;
            case ".vmd":
                OpenVMDFile(path);
                break;
            case ".coocoo3dscene":
                LoadScene(path);
                break;
            default:
                return false;
        }
        gameDriver.RequireRender(true);
        return true;
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
            if (editorContext.selectedObject.IsAlive && TryGetComponent(editorContext.selectedObject, out Components.AnimationStateComponent animationState))
            {
                animationState.motion = mainCaches.GetMotion(path);
            }
        }
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
