using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Coocoo3D.Extensions.FileLoader;

[Export("ResourceLoader")]
[Export(typeof(IEditorAccess))]
public class MyTextureLoader : IResourceLoader<Texture2D>, IEditorAccess, IDisposable
{
    public MainCaches mainCaches;
    public GraphicsContext graphicsContext;
    public GameDriverContext gameDriverContext;
    Texture2D loadingTexture;
    Texture2D errorTexture;
    public bool TryLoad(string path, out Texture2D texture)
    {
        var texture1 = new Texture2D();
        texture1.Status = GraphicsObjectStatus.loading;
        texture = texture1;
        TextureReplace(texture1);
        mainCaches.ProxyCall(new AsyncProxy
        {
            calls = async Task () =>
            {
                await Task.Yield();
                if (LoadTexture(path, out var uploader))
                {
                    mainCaches.ProxyCall(() =>
                    {
                        graphicsContext.UploadTexture(texture1, uploader);
                    });
                }
                else
                {
                    mainCaches.ProxyCall(() =>
                    {
                        texture1.Status = GraphicsObjectStatus.error;
                        TextureReplace(texture1);
                    });
                }
                gameDriverContext.RequireRender(false);
            },
            cost = 1
        });

        return true;
    }

    public void Initialize()
    {
        loadingTexture = GetTextureLoaded1(Path.GetFullPath("Assets/Textures/loading.png"));
        errorTexture = GetTextureLoaded1(Path.GetFullPath("Assets/Textures/error.png"));
    }

    public void Dispose()
    {
        loadingTexture?.Dispose();
        loadingTexture = null;
        errorTexture?.Dispose();
        errorTexture = null;
    }

    void TextureReplace(Texture2D texture)
    {
        if (texture.Status == GraphicsObjectStatus.loading)
            loadingTexture.RefCopyTo(texture);
        else if (texture.Status != GraphicsObjectStatus.loaded)
            errorTexture.RefCopyTo(texture);
    }


    Texture2D GetTextureLoaded1(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        var texture2D = new Texture2D();
        Uploader uploader = new Uploader();
        using var stream = File.OpenRead(path);
        Texture2DPack.LoadTexture(path, stream, uploader);
        mainCaches.ProxyCall(() =>
        {
            graphicsContext.UploadTexture(texture2D, uploader);
        });
        texture2D.Status = GraphicsObjectStatus.loaded;
        return texture2D;
    }


    static bool LoadTexture(string fileName, out Uploader uploader)
    {
        uploader = null;
        try
        {
            Stream stream = File.OpenRead(fileName);
            Uploader uploader1 = new Uploader();
            if (Texture2DPack.LoadTexture(fileName, stream, uploader1))
            {
                uploader = uploader1;
                return true;
            }
        }
        catch
        {

        }
        return false;
    }
}
