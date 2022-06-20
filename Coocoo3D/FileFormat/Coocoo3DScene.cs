using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Core;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;

namespace Coocoo3D.FileFormat
{
    public class CooSceneObject
    {
        public CooSceneObject()
        {

        }
        public CooSceneObject(GameObject obj)
        {
            name = obj.Name;
            position = obj.Transform.position;
            rotation = obj.Transform.rotation;
            scale = obj.Transform.scale;
        }
        public string type;
        public string path;
        public string name;
        public bool? skinning;
        public bool? enableIK;
        public Vector3 position;
        public Quaternion rotation = Quaternion.Identity;
        public Vector3 scale = Vector3.One;
        public Dictionary<string, string> properties;
        public Dictionary<string, _cooMaterial> materials;
        public CooSceneObjectVisual visual;
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
    }
    public class Coocoo3DScene
    {
        public int formatVersion = 1;
        public List<CooSceneObject> objects;

        static bool _func1<T>(ref Dictionary<string, T> dict, KeyValuePair<string, object> pair)
        {
            if (pair.Value is T _t1)
            {
                dict ??= new Dictionary<string, T>();
                dict[pair.Key] = _t1;
                return true;
            }
            return false;
        }
        public static Coocoo3DScene FromScene(Coocoo3DMain main)
        {
            Coocoo3DScene scene = new Coocoo3DScene();
            scene.objects = new List<CooSceneObject>();

            foreach (var obj in main.CurrentScene.gameObjects)
            {
                var renderer = obj.GetComponent<MMDRendererComponent>();
                if (renderer != null)
                {
                    CooSceneObject sceneObject = new CooSceneObject(obj);
                    sceneObject.type = "mmdModel";
                    sceneObject.path = renderer.meshPath;
                    sceneObject.properties = new Dictionary<string, string>();
                    sceneObject.properties.Add("motion", renderer.motionPath);
                    sceneObject.materials = new Dictionary<string, _cooMaterial>();
                    sceneObject.skinning = renderer.skinning;
                    sceneObject.enableIK = renderer.enableIK;
                    foreach (var material in renderer.Materials)
                    {
                        sceneObject.materials[material.Name] = Mat2Mat(material);
                    }
                    scene.objects.Add(sceneObject);
                }
                var meshRenderer = obj.GetComponent<MeshRendererComponent>();
                if (meshRenderer != null)
                {
                    CooSceneObject sceneObject = new CooSceneObject(obj);
                    sceneObject.type = "model";
                    sceneObject.path = meshRenderer.meshPath;
                    sceneObject.materials = new Dictionary<string, _cooMaterial>();
                    foreach (var material in meshRenderer.Materials)
                    {
                        sceneObject.materials[material.Name] = Mat2Mat(material);
                    }
                    scene.objects.Add(sceneObject);
                }
                var visual = obj.GetComponent<VisualComponent>();
                if (visual != null)
                {
                    CooSceneObject decalObject = new CooSceneObject(obj);
                    if (visual.UIShowType == Caprice.Display.UIShowType.Decal)
                        decalObject.type = "decal";
                    if (visual.UIShowType == Caprice.Display.UIShowType.Light)
                        decalObject.type = "lighting";
                    decalObject.visual = new CooSceneObjectVisual(Mat2Mat(visual.material));
                    scene.objects.Add(decalObject);
                }
            }

            return scene;
        }
        static void _func2<T>(Dictionary<string, T> dict, Dictionary<string, object> target)
        {
            if (dict != null)
                foreach (var f1 in dict)
                    target[f1.Key] = f1.Value;
        }
        public void ToScene(Coocoo3DMain main)
        {
            foreach (var obj in objects)
            {
                GameObject gameObject = GetGameObject(obj);
                if (obj.type == "mmdModel")
                {
                    string pmxPath = obj.path;
                    ModelPack modelPack = main.mainCaches.GetModel(pmxPath);

                    gameObject.LoadPmx(modelPack);
                    var renderer = gameObject.GetComponent<MMDRendererComponent>();
                    if (obj.skinning != null)
                        renderer.skinning = (bool)obj.skinning;
                    if (obj.enableIK != null)
                        renderer.enableIK = (bool)obj.enableIK;
                    if (obj.properties != null)
                    {
                        if (obj.properties.TryGetValue("motion", out string motion))
                        {
                            renderer.motionPath = motion;
                        }
                    }
                    if (obj.materials != null)
                    {
                        Mat2Mat(obj.materials, renderer.Materials);
                    }
                    main.CurrentScene.AddGameObject(gameObject);
                }
                else if (obj.type == "model")
                {
                    string path = obj.path;
                    ModelPack modelPack = main.mainCaches.GetModel(path);

                    modelPack.LoadMeshComponent(gameObject);
                    var renderer = gameObject.GetComponent<MeshRendererComponent>();

                    if (obj.materials != null)
                    {
                        Mat2Mat(obj.materials, renderer.Materials);
                    }
                    main.CurrentScene.AddGameObject(gameObject);
                }
                else if (obj.type == "lighting")
                {
                    VisualComponent lightingComponent = new VisualComponent();
                    lightingComponent.UIShowType = Caprice.Display.UIShowType.Light;
                    gameObject.AddComponent(lightingComponent);
                    if (obj.visual != null)
                    {
                        lightingComponent.material = Mat2Mat(obj.visual.material);
                    }
                    main.CurrentScene.AddGameObject(gameObject);
                }
                else if (obj.type == "decal")
                {
                    VisualComponent decalComponent = new VisualComponent();
                    decalComponent.UIShowType = Caprice.Display.UIShowType.Decal;
                    gameObject.AddComponent(decalComponent);
                    if (obj.visual != null)
                    {
                        decalComponent.material = Mat2Mat(obj.visual.material);
                    }
                    main.CurrentScene.AddGameObject(gameObject);
                }
            }
            main.GameDriverContext.RequireResetPhysics = true;
        }
        static void Mat2Mat(Dictionary<string, _cooMaterial> materials, List<RenderMaterial> renderMaterials)
        {
            foreach (var mat in renderMaterials)
            {
                if (!materials.TryGetValue(mat.Name, out _cooMaterial mat1)) continue;

                _func2(mat1.fValue, mat.Parameters);
                _func2(mat1.f2Value, mat.Parameters);
                _func2(mat1.f3Value, mat.Parameters);
                _func2(mat1.f4Value, mat.Parameters);
                _func2(mat1.iValue, mat.Parameters);
                _func2(mat1.i2Value, mat.Parameters);
                _func2(mat1.i3Value, mat.Parameters);
                _func2(mat1.i4Value, mat.Parameters);
                _func2(mat1.bValue, mat.Parameters);
                _func2(mat1.strValue, mat.Parameters);
            }
        }
        static RenderMaterial Mat2Mat(_cooMaterial material)
        {
            RenderMaterial mat = new RenderMaterial();

            _func2(material.fValue, mat.Parameters);
            _func2(material.f2Value, mat.Parameters);
            _func2(material.f3Value, mat.Parameters);
            _func2(material.f4Value, mat.Parameters);
            _func2(material.iValue, mat.Parameters);
            _func2(material.i2Value, mat.Parameters);
            _func2(material.i3Value, mat.Parameters);
            _func2(material.i4Value, mat.Parameters);
            _func2(material.bValue, mat.Parameters);
            _func2(material.strValue, mat.Parameters);
            return mat;
        }
        static _cooMaterial Mat2Mat(RenderMaterial material)
        {
            _cooMaterial material1 = new();

            foreach (var customValue in material.Parameters)
            {
                if (_func1(ref material1.fValue, customValue)) continue;
                if (_func1(ref material1.f2Value, customValue)) continue;
                if (_func1(ref material1.f3Value, customValue)) continue;
                if (_func1(ref material1.f4Value, customValue)) continue;
                if (_func1(ref material1.iValue, customValue)) continue;
                if (_func1(ref material1.i2Value, customValue)) continue;
                if (_func1(ref material1.i3Value, customValue)) continue;
                if (_func1(ref material1.i4Value, customValue)) continue;
                if (_func1(ref material1.bValue, customValue)) continue;
                if (_func1(ref material1.strValue, customValue)) continue;
                if (customValue.Value != null && customValue.Value.GetType().IsEnum)
                {
                    if (material1.strValue == null) material1.strValue = new Dictionary<string, string>();
                    material1.strValue[customValue.Key] = customValue.Value.ToString();
                }
            }
            return material1;
        }
        GameObject GetGameObject(CooSceneObject obj)
        {
            GameObject gameObject = new GameObject();

            gameObject.Name = obj.name ?? string.Empty;
            gameObject.Transform = new(obj.position, obj.rotation, obj.scale);
            return gameObject;
        }
    }
}
