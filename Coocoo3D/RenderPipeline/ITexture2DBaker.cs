using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline;

public interface ITexture2DBaker
{
    public bool Bake(Texture2D texture, RenderPipelineView renderPipelineView, ref object tag);
}
