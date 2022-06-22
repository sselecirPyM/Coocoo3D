using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class GenerateMipPass
    {
        string shader = "GenerateMipMap.hlsl";

        string rs = "Csu";

        public string input;

        public string output;


        public void Execute(RenderWrap renderWrap)
        {
            var texInput = renderWrap.GetRenderTexture2D(input);
            var texOutput = renderWrap.GetRenderTexture2D(output);
            int width = texInput.width;
            int height = texInput.height;
            renderWrap.SetRootSignature(rs);


            renderWrap.SetSRV(texInput, 0);
            renderWrap.SetUAV(texOutput, 0, 0);
            var writer = renderWrap.Writer;
            writer.Write(width);
            writer.Write(height);
            writer.SetCBV(0);
            renderWrap.Dispatch(shader, null, (width + 15) / 16, (height + 15) / 16);

            int x = width;
            int y = height;
            for (int i = 1; i < 9; i++)
            {
                x = (x + 1) / 2;
                y = (y + 1) / 2;

                writer.Write(x);
                writer.Write(y);
                writer.SetCBV(0);
                renderWrap.SetSRVLim(texOutput, i - 1, 0);
                renderWrap.SetUAV(texOutput, i, 0);
                renderWrap.Dispatch(shader, null, (x + 15) / 16, (y + 15) / 16);
            }
        }
    }
}
