using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class TAAPass
    {
        public string target;
        public string depth;

        public string history;
        public string historyDepth;

        public object[] cbv;

        public DebugRenderType DebugRenderType;

        public void Execute(RenderWrap renderWrap)
        {
            renderWrap.SetRootSignature("Csssu");

            List<(string, string)> keywords = new List<(string, string)>();
            keywords.Add(("ENABLE_TAA", "1"));
            if (DebugRenderType == DebugRenderType.TAA)
                keywords.Add(("DEBUG_TAA", "1"));

            var tex = renderWrap.GetTex2D(target);
            var writer = renderWrap.Writer;
            renderWrap.Write(cbv, writer);
            writer.SetCBV(0);

            renderWrap.SetSRVs(new string[] { depth, history, historyDepth });
            renderWrap.SetUAV(renderWrap.GetRenderTexture2D(target), 0);
            renderWrap.Dispatch("TAA.hlsl", keywords, (tex.width + 7) / 8, (tex.height + 7) / 8);
        }
    }
}
