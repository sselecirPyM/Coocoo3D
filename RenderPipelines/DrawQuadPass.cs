using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class DrawQuadPass : Pass
    {
        public string shader;

        public List<(string, string)> keywords = new();
        List<(string, string)> _keywords = new();

        public PSODesc psoDesc;

        public string rs;

        public object[][] cbvs;

        public bool clearRenderTarget = false;
        public bool clearDepth = false;

        public override void Execute(RenderHelper renderHelper)
        {

            RenderWrap renderWrap = renderHelper.renderWrap;
            _keywords.Clear();
            _keywords.AddRange(this.keywords);

            AutoMapKeyword(renderWrap, _keywords, null);

            renderWrap.SetRootSignature(rs);
            renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
            var desc = GetPSODesc(renderWrap, psoDesc);
            renderWrap.SetShader(shader, desc, _keywords);
            renderWrap.SetSRVs(srvs, null);

            var writer = renderWrap.Writer;
            if (cbvs != null)
                for (int i = 0; i < cbvs.Length; i++)
                {
                    object[] cbv1 = cbvs[i];
                    if (cbv1 == null) continue;
                    renderWrap.Write(cbv1, writer);
                    writer.SetCBV(i);
                }
            renderHelper.DrawQuad();
            writer.Clear();
            _keywords.Clear();
        }
    }
}
