using Coocoo3D.RenderPipeline;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Vortice.Mathematics;

namespace RenderPipelines.MetaRender;

public class ObjectProcessor
{
    List<PointLightData1> pointLightDatas = new();
    List<DirectionalLightData> directionalLightDatas = new();

    List<MeshRenderable1> meshRenderables = new();

    List<ParticleRenderable> particleRenderables = new();
    List<DecalRenderable> decalRenderables = new();

    Dictionary<int, ParticlesEx> particlesExs = new Dictionary<int, ParticlesEx>();

    HashSet<int> temp1 = new HashSet<int>();

    CameraData camera;

    void Clear()
    {
        pointLightDatas.Clear();
        directionalLightDatas.Clear();
        meshRenderables.Clear();
        particleRenderables.Clear();
        decalRenderables.Clear();
    }

    public void Process(MetaRenderContext metaRenderContext)
    {
        Clear();

        foreach (var obj in metaRenderContext.originData)
        {
            switch (obj)
            {
                case PointLightData1 pointLightData:
                    pointLightDatas.Add(pointLightData);
                    break;
                case DirectionalLightData directionalLightData:
                    directionalLightDatas.Add(directionalLightData);
                    break;
                case MeshRenderable1 renderable:
                    meshRenderables.Add(renderable);
                    break;
                case CameraData camera1:
                    camera = camera1;
                    break;
                case ParticleRenderable particle:
                    particleRenderables.Add(particle);
                    break;
                case DecalRenderable decalRenderable:
                    decalRenderables.Add(decalRenderable);
                    break;
            }
        }
        BoundingFrustum frustum = new BoundingFrustum(camera.vpMatrix);
        pointLightDatas.RemoveAll(u =>
        {
            return !frustum.Intersects(new BoundingSphere(u.Position, u.Range));
        });
        foreach (var renderer in meshRenderables)
        {
            ExpandoObject expandoObj = new ExpandoObject();
            expandoObj.TryAdd("Renderer", renderer);
            expandoObj.TryAdd("Camera", camera);
            expandoObj.TryAdd("Type", renderer);
            metaRenderContext.AddToRenderPool("DrawObject", expandoObj);
            int pointLightCount = 0;
            foreach (var dat in directionalLightDatas)
            {
                var directionnalLightData = dat;
                expandoObj.TryAdd("DirectionalLight", directionnalLightData);
                ExpandoObject obj1 = new ExpandoObject();
                obj1.TryAdd("Renderer", renderer);
                obj1.TryAdd("ShadowCaster", directionnalLightData);
                metaRenderContext.AddToRenderPool("DirectionalLight", obj1);
            }
            foreach (var dat in pointLightDatas)
            {
                if (pointLightCount >= 64)
                    break;
                var pointLightData = dat;
                expandoObj.TryAdd("PointLight" + pointLightCount, pointLightData);
                ExpandoObject obj1 = new ExpandoObject();
                obj1.TryAdd("Renderer", renderer);
                obj1.TryAdd("PointShadowCaster", pointLightData);
                metaRenderContext.AddToRenderPool("PointLight", obj1);
                pointLightCount++;
            }
        }
        foreach (var particle in particleRenderables)
        {
            if (!particlesExs.TryGetValue(particle.id, out var particlesEx))
            {
                particlesEx = new ParticlesEx();
                particlesExs.Add(particle.id, particlesEx);
            }
        }
        foreach (var pair in particlesExs)
            temp1.Add(pair.Key);
        temp1.ExceptWith(particleRenderables.Select(u => u.id));
        foreach (var i in temp1)
            particlesExs.Remove(i);


        foreach (var particle in particleRenderables)
        {
            if (particlesExs.TryGetValue(particle.id, out var ex))
            {
                ex.particleRenderable = particle;
                particle.particleProperties = ex.properties;
                ExpandoObject obj1 = new ExpandoObject();
                obj1.TryAdd("Particle", particle);
                metaRenderContext.AddToRenderPool("Particles", obj1);

            }
        }
        foreach (var pair in particlesExs)
        {
            pair.Value.Simulate(metaRenderContext.deltaTime);
        }
        foreach (var decal in decalRenderables)
        {
            if (!frustum.Intersects(new BoundingSphere(decal.transform.position, decal.transform.scale.Length())))
                continue;
            ExpandoObject obj1 = new ExpandoObject();
            obj1.TryAdd("Decal", decal);
            metaRenderContext.AddToRenderPool("Decal", obj1);
        }
    }
}
