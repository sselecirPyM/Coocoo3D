using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class BloomPass
    {
        string shader = "Bloom.hlsl";

        PSODesc psoDesc = new PSODesc()
        {
            cullMode = CullMode.None,
            renderTargetCount = 1,
        };

        string rs = "Cs";

        public object[][] cbvs;

        public string intermediaTexture;

        public bool clearRenderTarget = false;

        public string input;

        public string output;

        public void Execute(RenderWrap renderWrap)
        {
            if (cbvs == null || cbvs.Length < 1 ||  string.IsNullOrEmpty(input)) return;
            renderWrap.SetRootSignature(rs);

            renderWrap.CopyTexture(renderWrap.GetRenderTexture2D(output), renderWrap.GetTex2D(input));

            var intermedia1 = renderWrap.GetTex2D(intermediaTexture);
            renderWrap.SetSRVs(new[] { input }, null);
            cbvs[0][0] = intermedia1.width;
            cbvs[0][1] = intermedia1.height;
            var writer = renderWrap.Writer;
            for (int i = 0; i < cbvs.Length; i++)
            {
                object[] cbv1 = cbvs[i];
                if (cbv1 == null) continue;
                renderWrap.Write(cbv1, writer);
                writer.SetBufferImmediately(i);
            }
            renderWrap.SetRenderTarget(new[] { intermediaTexture }, null, clearRenderTarget, false);
            psoDesc.rtvFormat = intermedia1.format;
            psoDesc.blendState = BlendState.None;
            renderWrap.SetShader(shader, psoDesc, new[] { ("BLOOM_1", "1") });
            renderWrap.DrawQuad();

            var renderTarget = renderWrap.GetTex2D(output);
            renderWrap.SetSRVs(new[] { intermediaTexture }, null);
            cbvs[0][0] = renderTarget.width;
            cbvs[0][1] = renderTarget.height;
            for (int i = 0; i < cbvs.Length; i++)
            {
                object[] cbv1 = cbvs[i];
                if (cbv1 == null) continue;
                renderWrap.Write(cbv1, writer);
                writer.SetBufferImmediately(i);
            }
            renderWrap.SetRenderTarget(renderTarget, null, clearRenderTarget, false);
            psoDesc.rtvFormat = renderTarget.format;
            psoDesc.blendState = BlendState.Add;
            renderWrap.SetShader(shader, psoDesc, new[] { ("BLOOM_2", "1") });
            renderWrap.DrawQuad();
            writer.Clear();
        }
    }
}
