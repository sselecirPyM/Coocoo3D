using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;
using Caprice.Attributes;

namespace Coocoo3D.RenderPipeline
{
    internal static class RPUtil
    {
        public static void Texture2D(Dictionary<string, Texture2D> RTs, string rtName, RenderTarget rt, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (!RTs.TryGetValue(rtName, out var tex2d))
            {
                tex2d = new Texture2D();
                tex2d.Name = rtName;
                RTs[rtName] = tex2d;
            }
            Texture2D(tex2d, rt.Format, x, y, mips, graphicsContext);
        }

        public static void Texture2D(Texture2D tex2d, Format format, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (tex2d.width != x || tex2d.height != y || tex2d.mipLevels != mips || tex2d.GetFormat() != format)
            {
                if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                    tex2d.ReloadAsDSV(x, y, mips, format);
                else
                    tex2d.ReloadAsRTVUAV(x, y, mips, format);
                graphicsContext.UpdateRenderTexture(tex2d);
            }
        }

        public static void TextureCube(Dictionary<string, TextureCube> RTCs, string rtName, RenderTarget rt, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (!RTCs.TryGetValue(rtName, out var texCube))
            {
                texCube = new TextureCube();
                texCube.Name = rtName;
                RTCs[rtName] = texCube;
            }

            TextureCube(texCube, rt.Format, x, y, mips, graphicsContext);
        }

        public static void TextureCube(TextureCube texCube, Format format, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (texCube.width != x || texCube.height != y || texCube.mipLevels != mips || texCube.GetFormat() != format)
            {
                if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                    texCube.ReloadAsDSV(x, y, mips, format);
                else
                    texCube.ReloadAsRTVUAV(x, y, mips, format);
                graphicsContext.UpdateRenderTexture(texCube);
            }
        }

        public static void DynamicBuffer(Dictionary<string, GPUBuffer> dynamicBuffers, string rtName, int width, GraphicsContext graphicsContext)
        {
            if (!dynamicBuffers.TryGetValue(rtName, out var buffer))
            {
                buffer = new GPUBuffer();
                buffer.Name = rtName;
                dynamicBuffers[rtName] = buffer;
            }
            if (width != buffer.size)
            {
                buffer.size = width;
                graphicsContext.UpdateDynamicBuffer(buffer);
            }
        }

        public static void DynamicBuffer(GPUBuffer dynamicBuffer, int width, GraphicsContext graphicsContext)
        {
            if (width != dynamicBuffer.size)
            {
                dynamicBuffer.size = width;
                graphicsContext.UpdateDynamicBuffer(dynamicBuffer);
            }
        }
    }
}
