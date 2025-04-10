using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using System.IO;
using System.Numerics;

namespace Coocoo3D.Extensions.FileFormat
{
    public static class ModelExt
    {
        public static MeshRendererComponent LoadMesh(this ModelPack modelPack, Entity gameObject)
        {
            return LoadMesh(modelPack, gameObject, new Transform(Vector3.Zero, Quaternion.Identity));
        }
        public static (MMDRendererComponent, AnimationStateComponent) LoadPmx(this ModelPack modelPack, Scene scene)
        {
            var entity = scene.CreateEntity();
            var renderer = PMXFormatExtension.LoadPmx(entity, scene, modelPack, new Transform(Vector3.Zero, Quaternion.Identity));
            var animationState = entity.LoadAnimationState();
            return (renderer, animationState);
        }
        public static MeshRendererComponent LoadMesh(this ModelPack modelPack, Entity gameObject, Transform transform)
        {
            var meshRenderer = new MeshRendererComponent();
            gameObject.Add(transform);
            gameObject.Add(meshRenderer);
            gameObject.Add(new ObjectDescription()
            {
                Name = Path.GetFileName(modelPack.fullPath),
                Description = ""
            });
            meshRenderer.meshPath = modelPack.fullPath;
            meshRenderer.model = modelPack;
            meshRenderer.transform = new Transform(Vector3.Zero, Quaternion.Identity);
            foreach (var material in modelPack.Materials)
                meshRenderer.Materials.Add(material.GetClone());
            return meshRenderer;
        }
    }
}
