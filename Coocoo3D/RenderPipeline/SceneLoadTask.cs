using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Newtonsoft.Json;
using System.IO;

namespace Coocoo3D.RenderPipeline;

public class SceneLoadTask : ISyncTask
{
    public string path;

    public Scene Scene;

    public void Process(object state)
    {
        var scene = ReadJsonStream<Coocoo3DScene>(File.OpenRead(path));
        scene.ToScene(Scene, (MainCaches)state);
    }

    static T ReadJsonStream<T>(Stream stream)
    {
        JsonSerializer jsonSerializer = new JsonSerializer();
        jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
        using StreamReader reader1 = new StreamReader(stream);
        return jsonSerializer.Deserialize<T>(new JsonTextReader(reader1));
    }
}
