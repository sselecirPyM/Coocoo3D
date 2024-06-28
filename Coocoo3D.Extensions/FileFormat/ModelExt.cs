using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using DefaultEcs.Command;
using System.IO;
using System.Numerics;

namespace Coocoo3D.Extensions.FileFormat
{
    public static class ModelExt
    {
        public static MeshRendererComponent LoadMesh(this ModelPack modelPack, EntityRecord gameObject)
        {
            return LoadMesh(modelPack, gameObject, new Transform(Vector3.Zero, Quaternion.Identity));
        }
        public static (MMDRendererComponent, AnimationStateComponent) LoadPmx(this ModelPack modelPack, EntityRecord gameObject)
        {
            return PMXFormatExtension.LoadPmx(gameObject, modelPack, new Transform(Vector3.Zero, Quaternion.Identity));
        }
        public static MeshRendererComponent LoadMesh(this ModelPack modelPack, EntityRecord gameObject, Transform transform)
        {
            var meshRenderer = new MeshRendererComponent();
            gameObject.Set(transform);
            gameObject.Set(meshRenderer);
            gameObject.Set(new ObjectDescription()
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
