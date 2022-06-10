using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class HiZPass
    {
        public string input;
        public string output;
        static (string, string)[] keyword1 = new[] { ("INPUT_SELF", "1") };

        public void Execute(RenderWrap renderWrap)
        {
            var inputTexture = renderWrap.GetTex2D(input);
            var outputTexture = renderWrap.GetTex2D(output);
            renderWrap.SetRootSignature("Csu");
            renderWrap.SetSRV(inputTexture, 0);
            renderWrap.SetUAV(outputTexture, 0);
            renderWrap.Writer.Write(inputTexture.width);
            renderWrap.Writer.Write(inputTexture.height);
            renderWrap.Writer.Write(0);
            renderWrap.Writer.Write(0);
            renderWrap.Writer.Write(0);
            renderWrap.Writer.Write(0);
            renderWrap.Writer.SetBufferComputeImmediately(0);
            renderWrap.Dispatch("HiZ.hlsl", null, (inputTexture.width + 15) / 16, (inputTexture.height + 15) / 16);

            int x = inputTexture.width;
            int y = inputTexture.height;
            int offsetX = 0;
            int offsetY = 0;
            int t1 = 16;
            for (int i = 8 - 1; i >= 0; i--)
            {
                x = (x + 1) / 2;
                y = (y + 1) / 2;
                offsetX += t1 << i;
                offsetY += 0;

                renderWrap.Writer.Write(x);
                renderWrap.Writer.Write(y);
                renderWrap.Writer.Write(offsetX - (t1 << i));
                renderWrap.Writer.Write(offsetY);
                renderWrap.Writer.Write(offsetX);
                renderWrap.Writer.Write(offsetY);
                renderWrap.Writer.SetBufferComputeImmediately(0);
                renderWrap.SetUAV(outputTexture, 0);
                renderWrap.Dispatch("HiZ.hlsl", keyword1, (x + 15) / 16, (y + 15) / 16);

            }
        }
    }
}
