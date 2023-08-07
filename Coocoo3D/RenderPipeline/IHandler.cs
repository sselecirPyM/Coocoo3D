using System.Collections.Generic;

namespace Coocoo3D.RenderPipeline;

public interface IHandler<T>
{
    public bool Add(T task);

    public void Update();

    public List<T> Output { get; }
}
