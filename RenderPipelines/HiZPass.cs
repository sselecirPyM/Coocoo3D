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
            int width = inputTexture.width;
            int height = outputTexture.height;
            renderWrap.SetRootSignature("Csu");
            renderWrap.SetSRV(inputTexture, 0);
            renderWrap.SetUAV(outputTexture, 0);
            renderWrap.Writer.Write(width);
            renderWrap.Writer.Write(height);
            renderWrap.Writer.SetBufferComputeImmediately(0);
            renderWrap.Dispatch("HiZ.hlsl", null, (width + 15) / 16, (height + 15) / 16);

            int x = inputTexture.width;
            int y = inputTexture.height;
            for (int i = 1; i < 9; i++)
            {
                x = (x + 1) / 2;
                y = (y + 1) / 2;

                renderWrap.Writer.Write(x);
                renderWrap.Writer.Write(y);
                renderWrap.Writer.SetBufferComputeImmediately(0);
                renderWrap.SetSRVLim(outputTexture, i - 1, 0);
                renderWrap.SetUAV(outputTexture, i, 0);
                renderWrap.Dispatch("HiZ.hlsl", keyword1, (x + 15) / 16, (y + 15) / 16);
            }
        }
    }
}
