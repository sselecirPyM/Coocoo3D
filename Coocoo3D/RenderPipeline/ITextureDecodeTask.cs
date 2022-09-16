using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public interface ITextureDecodeTask : INavigableTask
    {
        public Texture2DPack TexturePack { get; set; }
        public Uploader Uploader { get; set; }
        public byte[] GetDatas();
        public string GetFileName();
    }
}
