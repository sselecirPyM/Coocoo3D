using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;

namespace RenderPipelines;

public class PostProcessPass : IDisposable
{
    BloomPass bloomPass = new BloomPass();

    SRGBConvertPass srgbConvert = new SRGBConvertPass();

    GenerateMipPass generateMipPass = new GenerateMipPass();

    public Texture2D inputColor;

    public Texture2D intermedia1;
    public Texture2D intermedia2;
    public Texture2D intermedia3;

    [UIShow(name: "启用泛光")]
    public bool EnableBloom;

    [UIDragFloat(0.01f, name: "泛光阈值")]
    public float BloomThreshold = 1.05f;
    [UIDragFloat(0.01f, name: "泛光强度")]
    public float BloomIntensity = 0.1f;

    public Texture2D output;

    public void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;

        if (EnableBloom)
        {
            generateMipPass.input = inputColor;
            generateMipPass.output = intermedia3;
            generateMipPass.context = renderHelper;
            generateMipPass.Execute();

            int r = 0;
            uint n = (uint)(inputColor.height / 1024);
            while (n > 0)
            {
                r++;
                n >>= 1;
            }
            bloomPass.intermediaTexture = intermedia1;
            bloomPass.mipLevel = r;
            bloomPass.inputSize = (inputColor.width / 2, inputColor.height / 2);

            bloomPass.input = intermedia3;
            bloomPass.output = intermedia2;
            bloomPass.BloomThreshold = BloomThreshold;
            bloomPass.BloomIntensity = BloomIntensity;
            bloomPass.Execute(renderHelper);
        }

        srgbConvert.inputColor = inputColor;//srgbConvert.srvs[0] = inputColor;
        srgbConvert.inputColor1 = intermedia2;//srgbConvert.srvs[1] = "intermedia2";

        renderWrap.SetRenderTarget(output, false);
        srgbConvert.context = renderHelper;
        srgbConvert.Execute();
    }

    public void Dispose()
    {
        bloomPass?.Dispose();
        bloomPass = null;
        srgbConvert?.Dispose();
        srgbConvert = null;
        generateMipPass?.Dispose();
        generateMipPass = null;
    }
}
