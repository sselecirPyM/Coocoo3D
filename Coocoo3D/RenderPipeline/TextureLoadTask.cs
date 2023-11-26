using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;

namespace Coocoo3D.RenderPipeline;

public class TextureLoadTask
{
    public TextureLoadTask(Texture2DPack pack)
    {
        this.TexturePack = pack;
    }

    public Texture2DPack TexturePack { get; set; }

    public KnownFile KnownFile { get; set; }

    public string Next { get; set; }

    public string CachePath => KnownFile.fullPath;

    public Uploader Uploader { get; set; }

    public void CacheInvalid()
    {
        Next = "ITextureDecodeTask";
    }

    public void OnEnterPipeline()
    {
        if (TexturePack != null)
            TexturePack.texture2D.Status = GraphicsObjectStatus.loading;
    }

    public void OnLeavePipeline()
    {
        if (TexturePack != null)
            TexturePack.texture2D.Status = TexturePack.Status;
    }
    public void OnError(Exception exception)
    {
        if (TexturePack != null)
            TexturePack.Status = GraphicsObjectStatus.error;
        Next = null;
    }

    public string GetFileName() => KnownFile.fullPath;
}
