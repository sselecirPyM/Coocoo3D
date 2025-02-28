using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Numerics;

namespace RenderPipelines.LambdaRenderers
{
    public class SkyboxRenderConfig
    {
        public PSODesc psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
        };

        public float SkyLightMultiple = 3;
        public Texture2D skybox;
        public Texture2D RenderTarget;


        public CameraData camera;
    }
}
