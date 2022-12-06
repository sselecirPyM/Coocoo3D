using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Coocoo3D.ResourceWrap;

namespace Coocoo3D.RenderPipeline
{
    public class ModelLoadTask : ISyncTask
    {
        public Scene scene;
        public string path;

        public void Process(object state)
        {
            var mainCaches = (MainCaches)state;
            ModelPack modelPack = mainCaches.GetModel(path);
            foreach (var tex in modelPack.textures)
                mainCaches.PreloadTexture(tex);

            var world = scene.recorder.Record(scene.world);

            if (modelPack.pmx != null)
            {
                var gameObject = world.CreateEntity();
                gameObject.LoadPmx(modelPack);
            }
            else
            {
                var entity = world.CreateEntity();
                modelPack.LoadMeshComponent(entity);
            }

        }
    }
}
