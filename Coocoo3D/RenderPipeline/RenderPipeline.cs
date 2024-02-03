using Caprice.Display;
using System;
using System.Collections.Generic;

namespace Coocoo3D.RenderPipeline;

public abstract class RenderPipeline
{
    public RenderWrap renderWrap;

    public abstract void BeforeRender();
    public abstract void Render();
    public abstract void AfterRender();

    public abstract IDictionary<UIShowType, ICloneable> materialTypes { get; }

    public virtual void OnResourceInvald(string name)
    {

    }
}
