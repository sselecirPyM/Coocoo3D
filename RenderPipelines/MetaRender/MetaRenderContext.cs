using System.Collections.Generic;

namespace RenderPipelines.MetaRender;

public class MetaRenderContext
{
    public List<object> processor;
    public List<object> originData = new List<object>();
    public List<object> renderQueue = new List<object>();
    public Dictionary<string, List<object>> renderPools = new Dictionary<string, List<object>>();

    public double deltaTime;

    public void Clear()
    {
        originData.Clear();
        renderPools.Clear();
        renderQueue.Clear();
    }

    public void AddToRenderPool(string category, object obj)
    {
        if (renderPools.TryGetValue(category, out var pool))
        {
            pool.Add(obj);
        }
        else
        {
            pool = new List<object> { obj };
            renderPools.Add(category, pool);
        }
    }
}
