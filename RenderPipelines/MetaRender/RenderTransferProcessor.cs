namespace RenderPipelines.MetaRender;

public class RenderTransferProcessor
{
    void Clear()
    {

    }

    public void Process(MetaRenderContext metaRenderContext)
    {
        Clear();
        if (metaRenderContext.renderPools.TryGetValue("DrawObject", out var pool))
        {
            pool.RemoveAll(u =>
            {
                return false;
            });
        }
    }
}
