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

        public object[] cbv =
        {
            null,//nameof(ViewProjection),
            null,//nameof(InvertViewProjection),
            null,//nameof(_ViewProjection),
            null,//nameof(_InvertViewProjection),
            null,//nameof(outputWidth),
            null,//nameof(outputHeight),
            null,//nameof(cameraFar),
            null,//nameof(cameraNear),
            null,//nameof(TAAFactor),
        };

        public DebugRenderType DebugRenderType;

        public void SetCamera(CameraData historyCamera, CameraData camera)
        {
            cbv[0] = camera.vpMatrix;
            cbv[1] = camera.pvMatrix;
            cbv[2] = historyCamera.vpMatrix;
            cbv[3] = historyCamera.pvMatrix;
            cbv[6] = camera.far;
            cbv[7] = camera.near;
        }

        public void SetProperties(int width, int height, float factor)
        {
            cbv[4] = width;
            cbv[5] = height;
            cbv[8] = factor;
        }
        List<(string, string)> keywords = new List<(string, string)>();

        public void Execute(RenderWrap renderWrap)
        {
            renderWrap.SetRootSignature("Csssu");
            keywords.Clear();
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
