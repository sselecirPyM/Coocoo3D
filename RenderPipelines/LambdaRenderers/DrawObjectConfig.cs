using Coocoo3DGraphics;
using Coocoo3DGraphics.Commanding;
using System;
using System.Collections.Generic;

namespace RenderPipelines.LambdaRenderers
{
    public class DrawObjectConfig
    {
        public List<Texture2D> RenderTargets = new List<Texture2D>();
        public Texture2D DepthStencil = null;

        public string shader;

        public List<(string, string)> keywords = new();
        public List<(string, string)> keywords2 = new();

        public PSODesc psoDesc;

        public Dictionary<int, object> additionalSRV = new Dictionary<int, object>();

        public bool DrawOpaque;
        public bool DrawTransparent;

        public Action<GraphicsCommandProxy> Binding;

        public Texture2D _ShadowMap;
        public Texture2D _Environment;
        public Texture2D _BRDFLUT;


        public PSODesc GetPSODesc(PSODesc desc)
        {
            var rtvs = RenderTargets;
            var dsv = DepthStencil;
            desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
            desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
            desc.renderTargetCount = rtvs.Count;

            return desc;
        }
    }
}
