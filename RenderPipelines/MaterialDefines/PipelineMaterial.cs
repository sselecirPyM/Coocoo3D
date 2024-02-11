using Caprice.Display;
using Coocoo3DGraphics;
using System.Numerics;

namespace RenderPipelines.MaterialDefines;

public class PipelineMaterial
{
    [UISlider(0.5f, 2.0f, name: "渲染倍数")]
    public float RenderScale = 1;

    [UIShow(name: "调试渲染")]
    public DebugRenderType DebugRenderType;

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



    public float Far;
    public float Near;
    public float Fov;
    public float AspectRatio;


    public Matrix4x4 ShadowMapVP;
    public Matrix4x4 ShadowMapVP1;

    public Vector3 LightDir;
    public Vector3 LightColor;

    public (int, int) OutputSize;
    public int RandomI;
}
