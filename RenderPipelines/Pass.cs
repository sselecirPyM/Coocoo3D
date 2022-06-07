using Coocoo3D.Present;
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

        public List<(string, string)> AutoKeyMap = new();

        protected void AutoMapKeyword(RenderWrap renderWrap, IList<(string, string)> keywords, RenderMaterial material)
        {
            foreach (var keyMap in AutoKeyMap)
            {
                if (true.Equals(renderWrap.GetIndexableValue(keyMap.Item1, material)))
                    keywords.Add((keyMap.Item2, "1"));
            }
        }

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
