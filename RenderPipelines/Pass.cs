using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public abstract class Pass
    {
        public string[] srvs;

        public string[] renderTargets;

        public string depthStencil;

        public abstract void Execute(RenderWrap renderWrap);

        public PSODesc GetPSODesc(RenderWrap renderWrap, PSODesc desc)
        {
            desc.rtvFormat = (renderTargets != null && renderTargets.Length > 0) ?
                renderWrap.GetRenderTexture2D(renderTargets[0]).GetFormat() : Vortice.DXGI.Format.Unknown;
            var dsv = renderWrap.GetRenderTexture2D(depthStencil);
            desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
            desc.renderTargetCount = (renderTargets != null) ? renderTargets.Length : 0;
            desc.inputLayout = InputLayout.mmd;

            return desc;
        }
    }
}
