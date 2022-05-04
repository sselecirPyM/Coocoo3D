using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public interface ITextureCubeBaker
    {
        public bool Bake(TextureCube texture, RenderWrap renderWrap, ref object tag);
    }
}
