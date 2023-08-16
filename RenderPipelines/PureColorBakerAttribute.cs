using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RenderPipelines;

public class PureColorBakerAttribute : RuntimeBakeAttribute, ITexture2DBaker
{
    static ushort[] quad = new ushort[] { 0, 1, 2, 2, 1, 3 };
    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
    {
        renderWrap.SetRenderTarget(texture, true);
        var psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
            rtvFormat = texture.GetFormat(),
            renderTargetCount = 1,
        };
        renderWrap.Writer.Write(Color);
        renderWrap.Writer.SetCBV(0);
        renderWrap.SetShader("PureColor.hlsl", psoDesc);
        renderWrap.graphicsContext.SetMesh(null, MemoryMarshal.Cast<ushort, byte>(quad), 0, 6);
        renderWrap.Draw(6, 0, 0);
        return true;
    }

    public Vector4 Color { get; }

    public PureColorBakerAttribute(float r, float g, float b, float a)
    {
        Color = new Vector4(r, g, b, a);
    }
}
