using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Collections.Generic;

namespace RenderPipelines;

public abstract class Pass
{
    public string[] srvs;

    public abstract void Execute(RenderHelper renderHelper);

    public List<(string, string)> AutoKeyMap = new();

    protected void AutoMapKeyword(RenderHelper renderHelper, IList<(string, string)> keywords, RenderMaterial material)
    {
        foreach (var keyMap in AutoKeyMap)
        {
            if (true.Equals(renderHelper.GetIndexableValue(keyMap.Item1, material)))
                keywords.Add((keyMap.Item2, "1"));
        }
    }

    public PSODesc GetPSODesc(RenderHelper renderHelper, PSODesc desc)
    {
        var rtvs = renderHelper.renderWrap.RenderTargets;
        var dsv = renderHelper.renderWrap.depthStencil;
        desc.rtvFormat = rtvs.Count > 0 ? rtvs[0].GetFormat() : Vortice.DXGI.Format.Unknown;
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
        desc.renderTargetCount = rtvs.Count;

        return desc;
    }
}
