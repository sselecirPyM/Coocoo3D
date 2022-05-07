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
        List<(string, string)> keywords2 = new();

        public List<(string, string)> AutoKeyMap = new();

        public PSODesc psoDesc;

        public string rs;

        public object[][] cbvs;

        public bool clearRenderTarget = false;
        public bool clearDepth = false;

        public override void Execute(RenderWrap renderWrap)
        {
            keywords2.Clear();
            keywords2.AddRange(this.keywords);
            foreach (var keyMap in AutoKeyMap)
            {
                if (true.Equals(renderWrap.GetIndexableValue(keyMap.Item1)))
                    keywords2.Add((keyMap.Item2, "1"));
            }

            renderWrap.SetRootSignature(rs);
            renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
            var desc = GetPSODesc(renderWrap, psoDesc);
            renderWrap.SetShader(shader, desc, keywords2);
            renderWrap.SetSRVs(srvs, null);

            var writer = renderWrap.Writer;
            if (cbvs != null)
                for (int i = 0; i < cbvs.Length; i++)
                {
                    object[] cbv1 = cbvs[i];
                    if (cbv1 == null) continue;
                    renderWrap.Write(cbv1, writer);
                    writer.SetBufferImmediately(i);
                }
            renderWrap.DrawQuad();
            writer.Clear();
            keywords2.Clear();
        }
    }
}
