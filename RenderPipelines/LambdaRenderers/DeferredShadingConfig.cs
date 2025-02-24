using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System.Collections.Generic;

namespace RenderPipelines.LambdaRenderers
{
    public class DeferredShadingConfig
    {
        public PSODesc GetPSODesc(RenderWrap renderWrap, PSODesc desc)
        {
            var rtvs = renderWrap.RenderTargets;
            desc.rtvFormat = rtvs[0].GetFormat();
            desc.renderTargetCount = rtvs.Count;

            return desc;
        }

        public Texture2D RenderTarget;

        public string shader = "DeferredFinal.hlsl";

        public List<(string, string)> keywords = new();
        public List<(string, string)> keywords2 = new();

        public PSODesc psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
            dsvFormat = Vortice.DXGI.Format.Unknown,
        };

        public object[][] cbvs;
        public List<PointLightData> pointLightDatas = new List<PointLightData>();

        public bool EnableFog;
        public bool EnableSSAO;
        public bool EnableSSR;
        public bool UseGI;
        public bool NoBackGround;

        public PipelineMaterial pipelineMaterial;
    }
}
