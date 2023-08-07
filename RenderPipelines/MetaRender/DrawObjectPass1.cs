using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Drawing;

namespace RenderPipelines.MetaRender;

public class DrawObjectPass1 : Pass
{
    public string shader;

    public List<(string, string)> keywords = new();
    List<(string, string)> keywords2 = new();

    public PSODesc psoDesc;

    public string rs;

    public bool clearRenderTarget = false;
    public bool clearDepth = false;

    public Rectangle? scissorViewport;

    public object[] CBVPerObject;

    public object[] CBVPerPass;

    public IEnumerable<MeshRenderable1> renderMeshes;

    public override void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;
        renderWrap.SetRootSignature(rs);
        renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);
        if (scissorViewport != null)
        {
            var rect = scissorViewport.Value;
            renderWrap.SetScissorRectAndViewport(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
        var desc = GetPSODesc(renderHelper, psoDesc);

        var writer = renderHelper.Writer;
        writer.Clear();
        if (CBVPerPass != null)
        {
            renderHelper.Write(CBVPerPass, writer);
            writer.SetCBV(2);
        }

        keywords2.Clear();
        Mesh mesh = null;
        Mesh mesh1 = null;
        foreach (var renderable in renderMeshes)
        {
            if (mesh != renderable.mesh || mesh1 != renderable.meshOverride)
            {
                mesh = renderable.mesh;
                mesh1 = renderable.meshOverride;
                renderHelper.SetMesh(renderable);
            }
            keywords2.AddRange(this.keywords);
            AutoMapKeyword(renderHelper, keywords2, renderable.properties);
            if (renderable.gpuSkinning)
            {
                keywords2.Add(new("SKINNING", "1"));
            }
            if (renderable.drawDoubleFace)
                desc.cullMode = CullMode.None;
            else
                desc.cullMode = CullMode.Back;
            renderWrap.SetShader(shader, desc, keywords2);

            CBVPerObject[0] = renderable.transform;

            renderHelper.Write(CBVPerObject, writer, renderable.properties);
            writer.SetCBV(1);

            renderWrap.SetSRVs(srvs, renderable.properties);

            renderHelper.Draw(renderable);
            keywords2.Clear();
        }
    }
}
