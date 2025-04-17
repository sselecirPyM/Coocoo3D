using Coocoo3D.Core;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using glTFLoader;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Coocoo3D.Extensions.FileLoader;

[Export(typeof(IEditorAccess))]
public class MyTextureLoader : IResourceLoader<Texture2D>, IEditorAccess, IDisposable
{
    public MainCaches mainCaches;
    public GraphicsContext graphicsContext;
    public GameDriverContext gameDriverContext;
    Texture2D loadingTexture;
    Texture2D errorTexture;

    public void Initialize()
    {
        mainCaches.AddLoader(this);
        loadingTexture = GetTextureLoaded1(Path.GetFullPath("Assets/Textures/loading.png"));
        errorTexture = GetTextureLoaded1(Path.GetFullPath("Assets/Textures/error.png"));
    }

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

    static string gltfTextureProtocol = "gltf://texture:";
    static bool LoadTexture(string fileName, out Uploader uploader)
    {
        uploader = null;
        try
        {
            Stream stream;
            string fileName1 = fileName;
            if (fileName.StartsWith(gltfTextureProtocol))
            {
                int stringDivision = fileName.IndexOf('|');
                int index = int.Parse(fileName[gltfTextureProtocol.Length..stringDivision]);
                string gltfFileName = fileName[(stringDivision + 1)..];
                var gltf = Interface.LoadModel(gltfFileName);
                stream = gltf.OpenImageFile(index, gltfFileName);
                fileName1 = ".png";
            }
            else
            {
                stream = File.OpenRead(fileName);
            }
            Uploader uploader1 = new Uploader();
            if (Texture2DPack.LoadTexture(fileName1, stream, uploader1))
            {
                uploader = uploader1;
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return false;
    }
}
