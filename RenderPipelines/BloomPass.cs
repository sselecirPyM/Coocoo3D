using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class BloomPass : Pass
    {
        string shader = "Bloom.hlsl";

        PSODesc psoDesc = new PSODesc()
        {
            cullMode = CullMode.None,
        };

        string rs = "Cs";

        public object[][] cbvs;

        public string intermediaTexture;

        public bool clearRenderTarget = false;

        public override void Execute(RenderWrap renderWrap)
        {
            if (cbvs == null || cbvs.Length < 1 || renderTargets == null || renderTargets.Length < 1) return;
            renderWrap.SetRootSignature(rs);


            var intermedia1 = renderWrap.GetTex2D(intermediaTexture);
            renderWrap.SetSRVs(new[] { renderTargets[0] }, null);
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
            var desc = GetPSODesc(renderWrap, psoDesc);
            desc.blendState = BlendState.None;
            renderWrap.SetShader(shader, desc, new[] { ("BLOOM_1", "1") });
            renderWrap.DrawQuad();

            var renderTarget = renderWrap.GetTex2D(renderTargets[0]);
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
            renderWrap.SetRenderTarget(renderTargets, null, clearRenderTarget, false);
            desc.blendState = BlendState.Add;
            renderWrap.SetShader(shader, desc, new[] { ("BLOOM_2", "1") });
            renderWrap.DrawQuad();
            writer.Clear();
        }
    }
}
