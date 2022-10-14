using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Collections.Generic;

namespace RenderPipelines;

public abstract class Pass
{
    public string[] srvs;

    public string[] renderTargets;

    public string depthStencil;

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
        desc.rtvFormat = (renderTargets != null && renderTargets.Length > 0) ?
            renderHelper.renderWrap.GetRenderTexture2D(renderTargets[0]).GetFormat() : Vortice.DXGI.Format.Unknown;
        var dsv = renderHelper.renderWrap.GetRenderTexture2D(depthStencil);
        desc.dsvFormat = dsv == null ? Vortice.DXGI.Format.Unknown : dsv.GetFormat();
        desc.renderTargetCount = (renderTargets != null) ? renderTargets.Length : 0;
        desc.inputLayout = InputLayout.Default;

        return desc;
    }
}
