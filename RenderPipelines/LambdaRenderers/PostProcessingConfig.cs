using Caprice.Display;
using Coocoo3DGraphics;

namespace RenderPipelines.LambdaRenderers
{
    public class PostProcessingConfig
    {
        [UIShow(name: "启用泛光")]
        public bool EnableBloom;

        [UIDragFloat(0.01f, name: "泛光阈值")]
        public float BloomThreshold = 1.05f;
        [UIDragFloat(0.01f, name: "泛光强度")]
        public float BloomIntensity = 0.1f;


        public Texture2D inputColor;

        public Texture2D intermedia1;
        public Texture2D intermedia2;
        public Texture2D intermedia3;

        public Texture2D output;
    }
}
