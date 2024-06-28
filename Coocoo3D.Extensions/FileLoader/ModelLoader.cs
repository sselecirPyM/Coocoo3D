using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace Coocoo3D.Extensions.FileLoader;

[Export("ResourceLoader")]
[Export(typeof(IEditorAccess))]
public class ModelLoader : IResourceLoader<ModelPack>, IEditorAccess
{
    public MainCaches mainCaches;
    public GraphicsContext graphicsContext;
    public GameDriverContext gameDriverContext;

    public bool TryLoad(string path, out ModelPack value)
    {
        path = Path.GetFullPath(path);
        var modelPack = new ModelPack();
        modelPack.fullPath = path;

        if (".pmx".Equals(Path.GetExtension(path), StringComparison.CurrentCultureIgnoreCase))
        {
            modelPack.LoadPMX(path);
        }
        else
        {
            modelPack.LoadModel(path);
        }

        var paths = new HashSet<string>(modelPack.textures);
        foreach (var material in modelPack.Materials)
        {
            var keys = new List<string>(material.Parameters.Keys);
            foreach (var key in keys)
            {
                object o = material.Parameters[key];
                if (o as string == ModelPack.whiteTextureReplace)
                {
                    material.Parameters[key] = mainCaches.GetTexturePreloaded(Path.GetFullPath("Assets/Textures/white.png", mainCaches.workDir));
                }
                else if (o is string path1 && paths.Contains(path1))
                {
                    material.Parameters[key] = mainCaches.GetTexturePreloaded(path1);
                }
            }
        }

        graphicsContext.UploadMesh(modelPack.GetMesh());
        value = modelPack;
        return true;
    }
}
