using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Extensions.FileFormat;
using Coocoo3D.RenderPipeline;
using System.ComponentModel.Composition;
using System.IO;

namespace Coocoo3D.Extensions.FileLoader;

[Export(typeof(IEditorAccess))]
public class MMDMotionLoader : IResourceLoader<MMDMotion>, IEditorAccess
{
    public MainCaches mainCaches;
    public void Initialize()
    {
        mainCaches.AddLoader(this);
    }
    public bool TryLoad(string path, out MMDMotion motion)
    {
        motion = default;
        var ext = Path.GetExtension(path).ToLower();
        if (ext != ".vmd")
        {
            return false;
        }
        using var stream = File.OpenRead(path);
        BinaryReader reader = new BinaryReader(stream);
        VMDFormat motionSet = VMDFormat.Load(reader);

        motion = new MMDMotion();
        motion.Load(motionSet);
        motion.fullPath = path;
        return true;
    }
}
