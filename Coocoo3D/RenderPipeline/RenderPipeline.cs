using Coocoo3D.Present;

namespace Coocoo3D.RenderPipeline;

public abstract class RenderPipeline
{
    public RenderPipelineView renderPipelineView;

    public abstract void Config(RenderPipelineView renderPipelineView);

    public virtual void OnResourceInvald(string name)
    {

    }

    public virtual object UIMaterial(RenderMaterial material)
    {
        return null;
    }
}
