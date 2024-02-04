using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;

namespace RenderPipelines;

public class DrawObjectPass
{
    public string shader;

    public string[] srvs;

    public List<(string, string)> keywords = new();
    List<(string, string)> keywords2 = new();

    public PSODesc psoDesc;

    public object[] CBVPerObject;

    public object[] CBVPerPass;

    public Dictionary<int, object> additionalSRV = new Dictionary<int, object>();

    public Func<MeshRenderable, bool> filter;

    public List<(string, string)> AutoKeyMap = new();

    void AutoMapKeyword(RenderHelper renderHelper, IList<(string, string)> keywords, RenderMaterial material)
    {
        foreach (var keyMap in AutoKeyMap)
        {
            if (true.Equals(renderHelper.GetIndexableValue(keyMap.Item1, material)))
                keywords.Add((keyMap.Item2, "1"));
        }
    }

    public PSODesc GetPSODesc(RenderWrap renderWrap, PSODesc desc)
    {
        var rtvs = renderWrap.RenderTargets;
        var dsv = renderWrap.depthStencil;
        desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
        desc.renderTargetCount = rtvs.Count;

        return desc;
    }

    public void Execute(RenderHelper context)
    {
        RenderWrap renderWrap = context.renderWrap;

        var desc = GetPSODesc(context.renderWrap, psoDesc);

        var writer = context.Writer;
        writer.Clear();
        if (CBVPerPass != null)
        {
            context.Write(CBVPerPass, writer);
            writer.SetCBV(2);
        }

        keywords2.Clear();
        foreach (var srv in additionalSRV)
        {
            if (srv.Value is byte[] data)
            {
                context.SetSRV(srv.Key, data);
            }
        }
        foreach (var renderable in context.MeshRenderables())
        {
            if (filter != null && !filter.Invoke(renderable))
                continue;
            keywords2.AddRange(this.keywords);
            AutoMapKeyword(context, keywords2, renderable.material);

            if (renderable.drawDoubleFace)
                desc.cullMode = CullMode.None;
            else
                desc.cullMode = CullMode.Back;
            renderWrap.SetShader(shader, desc, keywords2);

            CBVPerObject[0] = renderable.transform;

            context.Write(CBVPerObject, writer, renderable.material);
            writer.SetCBV(1);

            renderWrap.SetSRVs(srvs, renderable.material);

            context.Draw(renderable);
            keywords2.Clear();
        }
    }
}
