using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class TextureLoadTask : ICacheTask, INavigableTask, ITextureDecodeTask, IGpuUploadTask
    {
        public TextureLoadTask(Texture2D texture, Uploader uploader)
        {
            this.texture = texture;
            this.uploader = uploader;
        }
        public TextureLoadTask(Texture2DPack pack, Uploader uploader)
        {
            this.texture = pack.texture2D;
            this.uploader = uploader;
            this.pack = pack;
        }
        public TextureLoadTask(Texture2DPack pack)
        {
            this.texture = pack.texture2D;
            this.pack = pack;
        }

        public int width;
        public int height;
        public Format format;

        public Texture2D texture;
        public Uploader uploader;
        public Texture2DPack pack;

        public KnownFile knownFile;

        public Type Next { get; set; }

        public void SetCurrentHandleType(Type type)
        {
            Next = null;
        }

        public string CachePath => knownFile.fullPath;

        public Texture2D Texture => texture;

        public Uploader Uploader => uploader;

        public void CacheInvalid()
        {
            Next = typeof(ITextureDecodeTask);
        }

        public void OnEnterPipeline()
        {

        }

        public void OnLeavePipeline()
        {
            if (pack != null)
                texture.Status = pack.Status;
        }
        public void OnError(Exception exception)
        {
            if (pack != null)
                pack.Status = GraphicsObjectStatus.error;
            Next = null;
        }
    }
}
