using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;

namespace RenderPipelines;

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
                nameof(BloomThreshold),
                nameof(BloomIntensity),
                null,
                null,
            }
        }
    };

    public DrawQuadPass postProcess = new DrawQuadPass()
    {
        shader = "PostProcessing.hlsl",
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
            "intermedia2",
        },
        cbvs = new object[][]
        {
            new object []
            {

            }
        }
    };

    public GenerateMipPass generateMipPass = new GenerateMipPass();

    public string inputColor;

    public string inputDepth;

    [UIShow(name: "启用泛光")]
    public bool EnableBloom;

    [UIDragFloat(0.01f, name: "泛光阈值")]
    [Indexable]
    public float BloomThreshold = 1.05f;
    [UIDragFloat(0.01f, name: "泛光强度")]
    [Indexable]
    public float BloomIntensity = 0.1f;

    public string output;

    public void Execute(RenderHelper renderHelper)
    {
        renderHelper.PushParameters(this);
        RenderWrap renderWrap = renderHelper.renderWrap;
        var outputTexture = renderWrap.GetRenderTexture2D("intermedia2");
        renderWrap.ClearTexture(outputTexture);
        if (EnableBloom)
        {
            generateMipPass.input = inputColor;
            generateMipPass.output = "intermedia3";
            generateMipPass.Execute(renderWrap);
            var inputTexture = renderWrap.GetRenderTexture2D(inputColor);

            int r = 0;
            uint n = (uint)(inputTexture.height / 1024);
            while (n > 0)
            {
                r++;
                n >>= 1;
            }
            bloomPass.mipLevel = r;
            bloomPass.inputSize = (inputTexture.width / 2, inputTexture.height / 2);

            //bloomPass.input = inputColor;
            bloomPass.input = "intermedia3";
            bloomPass.output = "intermedia2";
            bloomPass.Execute(renderHelper);
            postProcess.srvs[0] = inputColor;
            postProcess.srvs[1] = "intermedia2";
        }
        else
        {
            postProcess.srvs[0] = inputColor;
            postProcess.srvs[1] = "intermedia2";
        }

        postProcess.renderTargets[0] = output;
        postProcess.Execute(renderHelper);
        renderHelper.PopParameters();
    }
}
