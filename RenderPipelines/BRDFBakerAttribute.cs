using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;

namespace RenderPipelines
{
    public class BRDFBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker
    {
        public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
        {
            renderWrap.SetRootSignature("");
            renderWrap.SetRenderTarget(texture, true);
            var psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
                rtvFormat = texture.GetFormat(),
                inputLayout = InputLayout.Default,
                renderTargetCount = 1,
            };
            renderWrap.SetShader("BRDFLUT.hlsl", psoDesc);
            renderWrap.DrawQuad();
            return true;
        }
    }
}
