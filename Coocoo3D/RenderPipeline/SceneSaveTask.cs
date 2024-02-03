using Coocoo3D.Core;
using Coocoo3D.FileFormat;
using Newtonsoft.Json;
using System.IO;

namespace Coocoo3D.RenderPipeline;

public class SceneSaveTask : ISyncTask
{
    public string path;

    public Scene Scene;

    public void Process(object state)
    {
        var scene = Coocoo3DScene.SaveScene(Scene);
        SaveJsonStream(new FileInfo(path).Create(), scene);
    }

    static void SaveJsonStream<T>(Stream stream, T val)
    {
        JsonSerializer jsonSerializer = new JsonSerializer();
        jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
        using StreamWriter writer = new StreamWriter(stream);
        jsonSerializer.Serialize(writer, val);
    }
}
