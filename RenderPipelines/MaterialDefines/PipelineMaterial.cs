using Coocoo3DGraphics;

namespace RenderPipelines.MaterialDefines;

public class PipelineMaterial
{

    public Texture2D depth;
    public Texture2D depth2;

    public Texture2D _ShadowMap;

    public Texture2D _HiZBuffer;

    public Texture2D gbuffer0;
    public Texture2D gbuffer1;
    public Texture2D gbuffer2;
    public Texture2D gbuffer3;

    public GPUBuffer GIBuffer;
    public GPUBuffer GIBufferWrite;
    public Texture2D _Environment;
    public Texture2D _BRDFLUT;

    public Texture2D _SkyBox;
}
