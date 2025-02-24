using Coocoo3D.Components;
using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.LambdaRenderers
{
    public class DrawDecalConfig
    {
        public List<Texture2D> RenderTargets = new List<Texture2D>();
        public Texture2D depthStencil = null;

        public PSODesc psoDesc = new PSODesc()
        {
            blendState = BlendState.PreserveAlpha,
            cullMode = CullMode.Front,
        };

        public Matrix4x4 ViewProjection;

        public IEnumerable<VisualComponent> Visuals;
    }
}
