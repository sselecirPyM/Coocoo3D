using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline;

public class GpuUploadTask : ISyncTask
{
    public Texture2D Texture { get; }
    public Uploader Uploader { get; }

    public GpuUploadTask(Texture2D texture, Uploader uploader)
    {
        Texture = texture;
        Uploader = uploader;
    }

    public void Process(object state)
    {
        var graphicsContext = (GraphicsContext)state;
        graphicsContext.UploadTexture(Texture, Uploader);
    }
}
