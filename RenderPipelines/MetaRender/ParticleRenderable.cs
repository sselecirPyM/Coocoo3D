using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.MetaRender;

public class ParticleRenderable
{
    public int id;
    public Matrix4x4 transform;
    public object material;
    public Dictionary<string, object> particleProperties;
}
