using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.Utility;
using System;

namespace RenderPipelines;

internal class CubeFrom2DAttribute : RuntimeBakeAttribute, ITexture2DBaker
{
    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
    {
        var tex = renderWrap.GetTex2D(Source);
        if (tex == null || tex.Status != GraphicsObjectStatus.loaded)
            return false;

        int width = texture.width;
        int height = texture.height;

        renderWrap.graphicsContext.SetCBVRSlot<int>(0, [width, height]);

        renderWrap.SetSRVs(new string[] { Source });
        renderWrap.SetUAV(0, texture, 0);
        renderWrap.Dispatch("ConvertToCube.hlsl", null, width / 8, height / 8, 6);
        for (int i = 1; i < texture.mipLevels; i++)
        {
            int mipPow = 1 << i;
            renderWrap.SetSRVMip(0, texture, i - 1);
            renderWrap.SetUAV(0, texture, i);

            ReadOnlySpan<int> cbvData1 = [(width / mipPow), (height / mipPow), (i - 1)];
            renderWrap.graphicsContext.SetCBVRSlot<int>(0, cbvData1);
            renderWrap.Dispatch("GenerateCubeMipMap.hlsl", null, width / mipPow / 8, height / mipPow / 8, 6);
        }
        return true;
    }

    public CubeFrom2DAttribute(string source)
    {
        Source = source;
    }

    public VariantComputeShader shader_ConvertToCube = new VariantComputeShader(
"""

""","csmain");

    public string Source { get; }
}
