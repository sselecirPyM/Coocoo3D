using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class BRDFBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker
{
    static ushort[] quad = new ushort[] { 0, 1, 2, 2, 1, 3 };
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
        renderWrap.graphicsContext.SetMesh(null, MemoryMarshal.Cast<ushort, byte>(quad), 0, 6);
        renderWrap.Draw(6, 0, 0);
        return true;
    }
}
