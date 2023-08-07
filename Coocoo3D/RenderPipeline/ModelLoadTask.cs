using Coocoo3D.Core;
using Coocoo3D.ResourceWrap;

namespace Coocoo3D.RenderPipeline;

public class ModelLoadTask : ISyncTask
{
    public Scene scene;
    public string path;

    public void Process(object state)
    {
        var mainCaches = (MainCaches)state;
        ModelPack modelPack = mainCaches.GetModel(path);

        var world = scene.recorder.Record(scene.world);

        if (modelPack.pmx != null)
        {
            var entity = world.CreateEntity();
            modelPack.LoadPmx(entity);
        }
        else
        {
            var entity = world.CreateEntity();
            modelPack.LoadMesh(entity);
        }
    }
}
