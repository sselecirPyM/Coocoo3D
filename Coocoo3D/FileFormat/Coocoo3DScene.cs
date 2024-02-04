using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.FileFormat;

public class CooSceneObject
{
    public CooSceneObject()
    {

    }
    public CooSceneObject(Entity obj)
    {
        TryGetComponent<Transform>(obj, out var transform);

        position = transform.position;
        rotation = transform.rotation;
        scale = transform.scale;
        if (TryGetComponent<ObjectDescription>(obj, out var objectDescription))
        {
            name = objectDescription.Name;
        }
        if (TryGetComponent<VisualComponent>(obj, out var visualComponent))
        {
            switch (visualComponent.material.Type)
            {
                case UIShowType.Decal:
                    type = "decal";
                    break;
                case UIShowType.Light:
                    type = "lighting";
                    break;
                case UIShowType.Particle:
                    type = "particle";
                    break;
            }
        }
    }
    public string type;
    public string path;
    public string name;
    public bool? skinning;
    public Vector3 position;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 scale = Vector3.One;
    public Dictionary<string, string> properties;
    public Dictionary<string, _cooMaterial> materials;
    public CooSceneObjectVisual visual;
    static T GetComponent<T>(Entity entity) where T : class
    {
        if (entity.Has<T>())
        {
            return entity.Get<T>();
        }
        return null;
    }
    static bool TryGetComponent<T>(Entity entity, out T value)
    {
        if (entity.Has<T>())
        {
            value = entity.Get<T>();
            return true;
        }
        else
        {
            value = default(T);
            return false;
        }
    }
}
public class CooSceneObjectVisual
{
    public _cooMaterial material;

    public CooSceneObjectVisual()
    {

    }

    public CooSceneObjectVisual(_cooMaterial material)
    {
        this.material = material;
    }
}
public class _cooMaterial
{
    public Dictionary<string, bool> bValue;
    public Dictionary<string, int> iValue;
    public Dictionary<string, (int, int)> i2Value;
    public Dictionary<string, (int, int, int)> i3Value;
    public Dictionary<string, (int, int, int, int)> i4Value;
    public Dictionary<string, float> fValue;
    public Dictionary<string, Vector2> f2Value;
    public Dictionary<string, Vector3> f3Value;
    public Dictionary<string, Vector4> f4Value;
    public Dictionary<string, string> strValue;
    public Dictionary<string, string> texValue;

    public void Add(string key, string value)
    {
        strValue ??= new Dictionary<string, string>();
        strValue[key] = value;
    }

    public void AddTexture(string key, string value)
    {
        texValue ??= new Dictionary<string, string>();
        texValue[key] = value;
    }
}
public class Coocoo3DScene
{
    public int formatVersion = 1;
    public List<CooSceneObject> objects;

    static bool toDTO<T>(ref Dictionary<string, T> dict, KeyValuePair<string, object> pair)
    {
        if (pair.Value is T _t1)
        {
            dict ??= new Dictionary<string, T>();
            dict[pair.Key] = _t1;
            return true;
        }
        return false;
    }
    public static Coocoo3DScene SaveScene(Scene saveScene)
    {
        Coocoo3DScene scene = new Coocoo3DScene();
        scene.objects = new List<CooSceneObject>();
        var world = saveScene.world;

        MainCaches mainCaches = saveScene.MainCaches;
        Dictionary<Texture2D, string> invert = new Dictionary<Texture2D, string>();
        foreach (var pair in mainCaches.TextureCaches)
        {
            invert[pair.Value.texture2D] = pair.Key;
        }

        foreach (var obj in world)
        {
            var renderer = GetComponent<MMDRendererComponent>(obj);
            var animationState = GetComponent<AnimationStateComponent>(obj);
            if (renderer != null)
            {
                CooSceneObject sceneObject = new CooSceneObject(obj);
                sceneObject.type = "mmdModel";
                sceneObject.path = renderer.meshPath;
                sceneObject.properties = new Dictionary<string, string>();
                sceneObject.properties.Add("motion", animationState.motionPath);
                sceneObject.materials = new Dictionary<string, _cooMaterial>();
                sceneObject.skinning = renderer.skinning;
                foreach (var material in renderer.Materials)
                {
                    sceneObject.materials[material.Name] = ToDTO(invert, material);
                }
                scene.objects.Add(sceneObject);
            }
            var meshRenderer = GetComponent<MeshRendererComponent>(obj);
            if (meshRenderer != null)
            {
                CooSceneObject sceneObject = new CooSceneObject(obj);
                sceneObject.type = "model";
                sceneObject.path = meshRenderer.meshPath;
                sceneObject.materials = new Dictionary<string, _cooMaterial>();
                foreach (var material in meshRenderer.Materials)
                {
                    sceneObject.materials[material.Name] = ToDTO(invert, material);
                }
                scene.objects.Add(sceneObject);
            }
            var visual = GetComponent<VisualComponent>(obj);
            if (visual != null)
            {
                CooSceneObject decalObject = new CooSceneObject(obj);
                decalObject.visual = new CooSceneObjectVisual(ToDTO(invert, visual.material));
                scene.objects.Add(decalObject);
            }
        }

        return scene;
    }
    static T GetComponent<T>(Entity entity) where T : class
    {
        if (entity.Has<T>())
        {
            return entity.Get<T>();
        }
        return null;
    }
    static void convert<T>(Dictionary<string, T> dict, Dictionary<string, object> target)
    {
        if (dict == null)
            return;
        foreach (var f1 in dict)
            target[f1.Key] = f1.Value;
    }


    public void ToScene(Scene currentScene, MainCaches caches)
    {
        var world = currentScene.recorder.Record(currentScene.world);

        foreach (var obj in objects)
        {
            var entity = world.CreateEntity();
            var transform = new Transform(obj.position, obj.rotation, obj.scale);
            if (obj.type == "mmdModel")
            {
                string pmxPath = obj.path;
                ModelPack modelPack = caches.GetModel(pmxPath);

                (var renderer, var animationState) = entity.LoadPmx(modelPack, transform);

                if (obj.skinning != null)
                    renderer.skinning = (bool)obj.skinning;
                if (obj.properties != null)
                {
                    if (obj.properties.TryGetValue("motion", out string motion))
                    {
                        animationState.motionPath = motion;
                    }
                }
                if (obj.materials != null)
                {
                    Mat2Mat(caches, obj.materials, renderer.Materials);
                }
            }
            else if (obj.type == "model")
            {
                string path = obj.path;
                ModelPack modelPack = caches.GetModel(path);

                var renderer = modelPack.LoadMesh(entity, transform);

                if (obj.materials != null)
                {
                    Mat2Mat(caches, obj.materials, renderer.Materials);
                }
            }
            else
            {
                Caprice.Display.UIShowType uiShowType = default;
                switch (obj.type)
                {
                    case "lighting":
                        uiShowType = Caprice.Display.UIShowType.Light;
                        break;
                    case "decal":
                        uiShowType = Caprice.Display.UIShowType.Decal;
                        break;
                    case "particle":
                        uiShowType = Caprice.Display.UIShowType.Particle;
                        break;
                    default:
                        continue;
                }
                VisualComponent component = new VisualComponent();
                component.material.Type = uiShowType;
                entity.Set(component);
                entity.Set(transform);
                if (obj.visual != null)
                {
                    component.material = FromDTO(caches, obj.visual.material);
                }
            }
            entity.Set(new ObjectDescription
            {
                Name = obj.name ?? string.Empty,
                Description = ""
            });
        }
    }
    void Mat2Mat(MainCaches mainCaches, Dictionary<string, _cooMaterial> materials, List<RenderMaterial> renderMaterials)
    {
        foreach (var mat in renderMaterials)
        {
            if (!materials.TryGetValue(mat.Name, out _cooMaterial mat1))
                continue;

            convert(mat1.fValue, mat.Parameters);
            convert(mat1.f2Value, mat.Parameters);
            convert(mat1.f3Value, mat.Parameters);
            convert(mat1.f4Value, mat.Parameters);
            convert(mat1.iValue, mat.Parameters);
            convert(mat1.i2Value, mat.Parameters);
            convert(mat1.i3Value, mat.Parameters);
            convert(mat1.i4Value, mat.Parameters);
            convert(mat1.bValue, mat.Parameters);
            convert(mat1.strValue, mat.Parameters);
            if (mat1.texValue != null)
                foreach (var texStr in mat1.texValue)
                {
                    mat.Parameters[texStr.Key] = mainCaches.GetTexturePreloaded(texStr.Value);
                }
        }
    }
    static RenderMaterial FromDTO(MainCaches mainCaches, _cooMaterial material)
    {
        RenderMaterial mat = new RenderMaterial();

        convert(material.fValue, mat.Parameters);
        convert(material.f2Value, mat.Parameters);
        convert(material.f3Value, mat.Parameters);
        convert(material.f4Value, mat.Parameters);
        convert(material.iValue, mat.Parameters);
        convert(material.i2Value, mat.Parameters);
        convert(material.i3Value, mat.Parameters);
        convert(material.i4Value, mat.Parameters);
        convert(material.bValue, mat.Parameters);
        convert(material.strValue, mat.Parameters);
        if (material.texValue != null)
            foreach (var texStr in material.texValue)
            {
                mat.Parameters[texStr.Key] = mainCaches.GetTexturePreloaded(texStr.Value);
            }
        return mat;
    }
    internal static _cooMaterial ToDTO(Dictionary<Texture2D, string> invertTexture, RenderMaterial material)
    {
        _cooMaterial material1 = new();

        foreach (var customValue in material.Parameters)
        {
            if (toDTO(ref material1.fValue, customValue)) continue;
            if (toDTO(ref material1.f2Value, customValue)) continue;
            if (toDTO(ref material1.f3Value, customValue)) continue;
            if (toDTO(ref material1.f4Value, customValue)) continue;
            if (toDTO(ref material1.iValue, customValue)) continue;
            if (toDTO(ref material1.i2Value, customValue)) continue;
            if (toDTO(ref material1.i3Value, customValue)) continue;
            if (toDTO(ref material1.i4Value, customValue)) continue;
            if (toDTO(ref material1.bValue, customValue)) continue;
            if (toDTO(ref material1.strValue, customValue)) continue;
            if (customValue.Value == null)
                continue;

            if (customValue.Value is Texture2D texture2d)
            {
                if (invertTexture.TryGetValue(texture2d, out string str))
                {
                    material1.AddTexture(customValue.Key, str);
                }
                else
                {

                }
            }
            else if (customValue.Value.GetType().IsEnum)
            {
                material1.Add(customValue.Key, customValue.Value.ToString());
            }
        }
        return material1;
    }
}
