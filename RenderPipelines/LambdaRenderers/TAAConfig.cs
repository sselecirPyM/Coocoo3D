using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using Coocoo3DGraphics.Commanding;
using System;

namespace RenderPipelines.LambdaRenderers
{
    public class TAAConfig
    {
        public Texture2D target;
        public Texture2D depth;

        public Texture2D history;
        public Texture2D historyDepth;

        public bool EnableTAA;
        public float TAAFactor = 0.3f;

        public CameraData historyCamera;
        public CameraData camera;

        public Action<CBVProxy> cbvBinding;
        public DebugRenderType DebugRenderType;
    }
}
