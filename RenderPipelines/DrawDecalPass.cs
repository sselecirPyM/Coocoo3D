using Coocoo3D.Components;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Mathematics;

namespace RenderPipelines
{
    public class DrawDecalPass : Pass
    {
        public string shader;

        public List<(string, string)> keywords = new();
        List<(string, string)> keywords2 = new();

        public List<(string, string)> AutoKeyMap = new();

        public PSODesc psoDesc;

        public bool enableVS = true;
        public bool enablePS = true;
        public bool enableGS = false;

        public string rs;

        public bool clearRenderTarget = false;
        public bool clearDepth = false;

        public Rectangle? scissorViewport;

        public object[] CBVPerObject;

        public object[] CBVPerPass;

        public Matrix4x4 viewProj;

        public Func<RenderWrap, VisualComponent, List<(string, string)>, bool> filter;

        public override void Execute(RenderWrap renderWrap)
        {
            renderWrap.SetRootSignature(rs);
            renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
            if (scissorViewport != null)
            {
                var rect = scissorViewport.Value;
                renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
            var desc = GetPSODesc(renderWrap, psoDesc);

            var writer = renderWrap.Writer;
            writer.Clear();
            if (CBVPerPass != null)
            {
                renderWrap.Write(CBVPerPass, writer);
                writer.SetBufferImmediately(1);
            }
            BoundingFrustum frustum = new(viewProj);

            keywords2.Clear();
            foreach (var renderable in renderWrap.visuals)
            {
                if (renderable.UIShowType != Caprice.Display.UIShowType.Decal)
                    continue;

                if (!frustum.Intersects(new BoundingSphere(renderable.transform.position, renderable.transform.scale.Length())))
                    continue;

                keywords2.AddRange(this.keywords);
                if (filter != null && !filter.Invoke(renderWrap, renderable, keywords2)) continue;
                foreach (var keyMap in AutoKeyMap)
                {
                    if (true.Equals(renderWrap.GetIndexableValue(keyMap.Item1, renderable.material)))
                        keywords2.Add((keyMap.Item2, "1"));
                }

                renderWrap.SetShader(shader, desc, keywords2, enableVS, enablePS, enableGS);

                Matrix4x4 m = renderable.transform.GetMatrix() * viewProj;
                Matrix4x4.Invert(m, out var im);
                CBVPerObject[0] = m;
                CBVPerObject[1] = im;

                renderWrap.Write(CBVPerObject, writer, renderable.material);
                writer.SetBufferImmediately(0);

                renderWrap.SetSRVs(srvs, renderable.material);

                renderWrap.DrawCube();
                keywords2.Clear();
            }
        }
    }
}
