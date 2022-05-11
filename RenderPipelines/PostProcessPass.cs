using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class PostProcessPass
    {
        public BloomPass bloomPass = new BloomPass()
        {
            intermediaTexture = "intermedia1",
            cbvs = new object[][]
            {
                new object[]
                {
                    null,
                    null,
                    "BloomThreshold",
                    "BloomIntensity",
                }
            }
        };

        public DrawQuadPass postProcess = new DrawQuadPass()
        {
            shader = "PostProcessing.hlsl",
            rs = "Cs",
            renderTargets = new string[]
            {
                null
            },
            //depthStencil = null,
            psoDesc = new PSODesc()
            {
                blendState = BlendState.None,
                cullMode = CullMode.None,
            },
            srvs = new string[]
            {
                null,
            },
            cbvs = new object[][]
            {
                new object []
                {

                }
            }
        };

        public string inputColor;

        public string inputDepth;

        public bool EnableBloom;

        public string output;

        public void Execute(RenderWrap renderWrap)
        {
            if (EnableBloom)
            {
                bloomPass.input = inputColor;
                bloomPass.output = "intermedia2";
                bloomPass.Execute(renderWrap);
                postProcess.srvs[0] = "intermedia2";
            }
            else
            {
                postProcess.srvs[0] = inputColor;
            }

            postProcess.renderTargets[0] = output;
            postProcess.Execute(renderWrap);
        }
    }
}
