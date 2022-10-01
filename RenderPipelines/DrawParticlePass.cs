using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public class DrawParticlePass : Pass
    {
        public string shader;

        public List<(string, string)> keywords = new();
        List<(string, string)> _keywords = new();

        public IReadOnlyList<(RenderMaterial, ParticleHolder)> Particles;

        public Matrix4x4 viewProj;

        public PSODesc psoDesc;

        public string rs;

        public object[] cbvs;

        public bool clearRenderTarget = false;
        public bool clearDepth = false;

        public override void Execute(RenderHelper renderHelper)
        {
            RenderWrap renderWrap = renderHelper.renderWrap;
            if (Particles.Count == 0)
                return;
            _keywords.Clear();
            _keywords.AddRange(this.keywords);

            AutoMapKeyword(renderWrap, _keywords, null);

            renderWrap.SetRootSignature(rs);
            renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);

            foreach (var particle in Particles)
            {
                DrawParticle(renderHelper, particle.Item1, particle.Item2);
            }
            _keywords.Clear();
        }

        void DrawParticle(RenderHelper renderHelper, RenderMaterial material, ParticleHolder particle)
        {
            RenderWrap renderWrap = renderHelper.renderWrap;
            var desc = GetPSODesc(renderWrap, psoDesc);
            object particleBlendMode = renderWrap.GetIndexableValue("ParticleBlendMode", material);
            if (particleBlendMode is BlendMode.Add)
                desc.blendState = BlendState.Add;
            else
                desc.blendState = BlendState.Alpha;
            renderWrap.SetShader(shader, desc, _keywords);

            var writer = renderWrap.Writer;
            writer.Write(particle.transform.GetMatrix());
            writer.Write(viewProj);
            renderWrap.Write(cbvs, writer, material);
            writer.Write(1.0f);
            for (int i = 0; i < particle.positions.Count; i++)
            {
                writer.Write(particle.positions[i]);
                writer.Write(particle.scales[i]);
            }
            writer.SetCBV(0);
            int count = particle.positions.Count;
            if (count == 0) return;
            renderWrap.SetSRVs(srvs, material);

            renderHelper.DrawQuad(count);
            writer.Clear();
        }
    }
}
