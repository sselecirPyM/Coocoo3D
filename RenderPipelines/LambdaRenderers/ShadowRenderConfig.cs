using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace RenderPipelines.LambdaRenderers
{
    public struct RenderDepthToViewport
    {
        public Matrix4x4 viewProjection;
        public Texture2D RenderTarget;
        public Rectangle Rectangle;
    }
    public class ShadowRenderConfig
    {
        public PSODesc psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
            depthBias = 2000,
            slopeScaledDepthBias = 1.5f,
        };

        public List<MeshRenderable<ModelMaterial>> renderables = new List<MeshRenderable<ModelMaterial>>();

        public List<RenderDepthToViewport> viewports = new List<RenderDepthToViewport>();
    }
}
