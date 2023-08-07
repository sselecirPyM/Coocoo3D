using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using RenderPipelines.MaterialDefines;
using System.Collections.Generic;
using System.Numerics;

namespace RenderPipelines.MetaRender;

public class DrawParticlePass1 : Pass
{
    List<(string, string)> _keywords = new();

    public IReadOnlyList<ParticleRenderable> Particles;

    PSODesc psoDesc = new PSODesc()
    {
        blendState = BlendState.Alpha,
        cullMode = CullMode.None,
    };

    string shader = "Particle.hlsl";
    string rs = "Css";

    public bool clearRenderTarget = false;
    public bool clearDepth = false;


    float Far;
    float Near;
    Vector3 CameraLeft;
    Vector3 CameraDown;
    Matrix4x4 ViewProjection;
    public void SetCamera(CameraData camera)
    {
        Far = camera.far;
        Near = camera.near;
        //Fov = camera.Fov;
        //AspectRatio = camera.AspectRatio;

        ViewProjection = camera.vpMatrix;
        //View = camera.vMatrix;
        //Projection = camera.pMatrix;
        //InvertViewProjection = camera.pvMatrix;
        //CameraPosition = camera.Position;


        Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-camera.Angle.Y, -camera.Angle.X, -camera.Angle.Z);
        CameraLeft = Vector3.Transform(-Vector3.UnitX, rotateMatrix);
        CameraDown = Vector3.Transform(-Vector3.UnitY, rotateMatrix);
        //CameraBack = Vector3.Transform(-Vector3.UnitZ, rotateMatrix);
    }

    public override void Execute(RenderHelper renderHelper)
    {
        RenderWrap renderWrap = renderHelper.renderWrap;
        if (Particles.Count == 0)
            return;
        _keywords.Clear();

        AutoMapKeyword(renderHelper, _keywords, null);

        renderWrap.SetRootSignature(rs);
        renderWrap.SetRenderTarget(renderTargets, depthStencil, clearRenderTarget, clearDepth);

        foreach (var particle in Particles)
        {
            DrawParticle(renderHelper, particle);
        }
        _keywords.Clear();
    }

    void DrawParticle(RenderHelper renderHelper, ParticleRenderable particle)
    {
        TryGetValue(particle.particleProperties, "Position", out IList<Vector3> positions);
        TryGetValue(particle.particleProperties, "Scale", out IList<Vector2> scales);
        TryGetValue(particle.particleProperties, "Lifetime", out IList<float> lifetime);
        TryGetValue(particle.particleProperties, "MaxLifetime", out IList<float> maxLifetime);

        if (positions == null || scales == null || lifetime == null || maxLifetime == null)
            return;

        int count = positions.Count;
        if (count == 0)
            return;
        RenderWrap renderWrap = renderHelper.renderWrap;
        var desc = GetPSODesc(renderHelper, psoDesc);

        renderWrap.SetShader(shader, desc, _keywords);
        var material = (ParticleMaterial)particle.material;
        var writer = renderHelper.Writer;
        //writer.Write(particle.transform);
        writer.Write(ViewProjection);
        writer.Write(material.Color);
        writer.Write(Far);
        writer.Write(Near);
        writer.Write(CameraLeft);
        writer.Write(CameraDown);
        writer.Write(1.0f);
        for (int i = 0; i < positions.Count; i++)
        {
            Vector4 color = material.Color;
            writer.Write(positions[i]);
            writer.Write(scales[i].Length());
            if (lifetime[i] / maxLifetime[i] < 0.3f)
                color.W *= lifetime[i] / maxLifetime[i] * 3.33333f;

            writer.Write(color);
        }
        writer.SetCBV(0);
        var depth = renderWrap.GetTex2DFallBack(srvs[0]);
        renderWrap.SetSRV(depth, 0);
        renderWrap.SetSRV(material.ParticleTexture, 1);
        //renderWrap.SetSRVs(srvs, material);

        renderHelper.DrawQuad(count);
        writer.Clear();
    }

    bool TryGetValue<T>(IDictionary<string, object> dict, string key, out T value) where T : class
    {
        if (dict.TryGetValue(key, out object val))
        {
            value = val as T;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }
}
