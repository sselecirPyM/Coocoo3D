using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class TextureLoadTask : ICacheTask, INavigableTask, IDiskLoadTask, ITextureDecodeTask
    {
        public TextureLoadTask(Texture2D texture, Uploader uploader)
        {
            this.texture = texture;
            this.Uploader = uploader;
        }
        public TextureLoadTask(Texture2DPack pack, Uploader uploader)
        {
            this.texture = pack.texture2D;
            this.Uploader = uploader;
            this.TexturePack = pack;
        }
        public TextureLoadTask(Texture2DPack pack)
        {
            this.texture = pack.texture2D;
            this.TexturePack = pack;
        }

        public int width;
        public int height;
        public Format format;

        public Texture2D texture;
        public Texture2DPack TexturePack { get; set; }

        public KnownFile KnownFile { get; set; }

        public Type Next { get; set; }

        public byte[] Datas { get; set; }

        public void SetCurrentHandleType(Type type)
        {
            switch (type.Name)
            {
                case "IDiskLoadTask":
                    Next = typeof(ITextureDecodeTask);
                    break;
                default:
                    Next = null;
                    break;
            }
        }

        public string CachePath => KnownFile.fullPath;

        public Texture2D Texture => texture;

        public Uploader Uploader { get; set; }

        public string FileName { get; set; }

        public void CacheInvalid()
        {
            Next = typeof(IDiskLoadTask);
        }

        public void OnEnterPipeline()
        {
            if (texture != null)
                texture.Status = GraphicsObjectStatus.loading;
        }

        public void OnLeavePipeline()
        {
            if (TexturePack != null)
                texture.Status = TexturePack.Status;
        }
        public void OnError(Exception exception)
        {
            if (TexturePack != null)
                TexturePack.Status = GraphicsObjectStatus.error;
            Next = null;
        }

        public byte[] GetDatas() => Datas;

        public string GetFileName() => KnownFile.fullPath;
    }
}
