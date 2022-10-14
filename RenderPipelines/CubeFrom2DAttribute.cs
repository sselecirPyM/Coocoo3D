using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;

namespace RenderPipelines;

internal class CubeFrom2DAttribute : RuntimeBakeAttribute, ITextureCubeBaker
{
    public bool Bake(TextureCube texture, RenderWrap renderWrap, ref object tag)
    {
        var tex = renderWrap.GetTex2D(Source);
        if (tex == null || tex.Status != GraphicsObjectStatus.loaded)
            return false;
        renderWrap.SetRootSignature("Csu");
        int width = texture.width;
        int height = texture.height;
        var writer = renderWrap.Writer;
        writer.Write(width);
        writer.Write(height);
        writer.SetCBV(0);
        renderWrap.SetSRVs(new string[] { Source });
        renderWrap.SetUAV(texture, 0, 0);
        renderWrap.Dispatch("ConvertToCube.hlsl", null, width / 8, height / 8, 6);
        for (int i = 1; i < texture.mipLevels; i++)
        {
            int mipPow = 1 << i;
            renderWrap.SetSRVLim(texture, i - 1, 0);
            renderWrap.SetUAV(texture, i, 0);
            writer.Write(width / mipPow);
            writer.Write(height / mipPow);
            writer.Write(i - 1);
            writer.SetCBV(0);
            renderWrap.Dispatch("GenerateCubeMipMap.hlsl", null, width / mipPow / 8, height / mipPow / 8, 6);
        }
        return true;
    }

    public CubeFrom2DAttribute(string source)
    {
        Source = source;
    }
    public string Source { get; }
}
