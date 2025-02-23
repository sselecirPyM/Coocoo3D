using Coocoo3DGraphics;
using RenderPipelines.LambdaPipe;
using System;
using Vortice.DXGI;

namespace RenderPipelines.LambdaRenderers
{
    public class TextureResourceProvider : IPipelineResourceProvider, IDisposable
    {
        public RenderHelper RenderHelper;


        public void BeforeRender()
        {
            if (shadowMap != null)
                RenderHelper.renderWrap.graphicsContext.ClearDSV(shadowMap);
        }

        Texture2D shadowMap;
        public Texture2D GetShadowMap()
        {
            if (shadowMap == null)
            {
                shadowMap = new Texture2D();
                Texture2D(shadowMap, Format.D32_Float, 4096, 4096, 1, 1, RenderHelper.renderWrap.graphicsContext);
            }

            return shadowMap;
        }

        public void Dispose()
        {
            shadowMap?.Dispose();
            shadowMap = null;
        }

        static void Texture2D(Texture2D tex2d, Format format, int x, int y, int mips, int arraySize, GraphicsContext graphicsContext)
        {
            if (tex2d.width != x || tex2d.height != y || tex2d.mipLevels != mips || tex2d.GetFormat() != format)
            {
                if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                    tex2d.ReloadAsDSV(x, y, mips, format);
                else
                    tex2d.ReloadAsRTVUAV(x, y, mips, arraySize, format);
                graphicsContext.UpdateRenderTexture(tex2d);
            }
        }
    }
}
