using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.LambdaRenderers
{
    public class DrawGBufferConfig
    {
        public List<Texture2D> RenderTargets = new List<Texture2D>();
        public Texture2D DepthStencil = null;

        public string shader = "DeferredGBuffer.hlsl";

        public List<(string, string)> keywords2 = new();

        public PSODesc psoDesc = new PSODesc()
        {
            blendState = BlendState.None,
            cullMode = CullMode.None,
        };

        public Matrix4x4 viewProjection;

        public Vector3 CameraLeft;
        public Vector3 CameraDown;

        public PSODesc GetPSODesc(RenderHelper renderHelper, PSODesc desc)
        {
            var rtvs = renderHelper.renderWrap.RenderTargets;
            var dsv = renderHelper.renderWrap.depthStencil;
            desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
            desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
            desc.renderTargetCount = rtvs.Count;

            return desc;
        }
    }
}
