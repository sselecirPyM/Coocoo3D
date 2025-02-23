using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;

namespace RenderPipelines.LambdaRenderers
{
    public class TAAConfig
    {
        public Texture2D target;
        public Texture2D depth;

        public Texture2D history;
        public Texture2D historyDepth;

        [UIShow(name: "启用TAA抗锯齿")]
        public bool EnableTAA;

        [UIDragFloat(0.01f, name: "混合系数")]
        public float TAAFactor = 0.3f;

        public CameraData historyCamera;
        public CameraData camera;

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
    }
}
