using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Extensions.FileFormat;
using System.ComponentModel.Composition;
using System.IO;

namespace Coocoo3D.Extensions.FileLoader;

[Export("ResourceLoader")]
public class MMDMotionLoader : IResourceLoader<MMDMotion>
{
    public bool TryLoad(string path,out MMDMotion motion)
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
        return true;
    }
}
