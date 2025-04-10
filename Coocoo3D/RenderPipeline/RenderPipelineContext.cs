using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.RenderPipeline;

public class RenderPipelineContext : IDisposable
{
    public Scene scene;

    public MainCaches mainCaches;

    public bool recording = false;

    public double Time;
    public double DeltaTime;
    public double RealDeltaTime;
    public CameraData CameraData;
    #region Collect data

    public List<MMDRendererComponent> renderers = new();
    //public List<MeshRendererComponent> meshRenderers = new();

    //QueryDescription visualQuery = new QueryDescription().WithAll<VisualComponent, Transform>();
    QueryDescription rendererQuery = new QueryDescription().WithAll<MMDRendererComponent, Transform>();

    public void FrameBegin()
    {

        //meshRenderers.Clear();
        //meshRenderers.AddRange(scene.meshRenderers);
        //foreach(var entity in entities)
        //{
        //    if (TryGetComponent(entity, out MMDRendererComponent renderer))
        //    {
        //        renderer.SetTransform(entity.Get<Transform>());
        //        renderers.Add(renderer);
        //    }
        //    if (TryGetComponent(entity, out MeshRendererComponent meshRenderer))
        //    {
        //        meshRenderer.transform = entity.Get<Transform>();
        //        meshRenderers.Add(meshRenderer);
        //    }
        //}


        renderers.Clear();
        scene.world.Query(rendererQuery, (ref MMDRendererComponent renderer,ref Transform transform) =>
        {
            //renderer.SetTransform(transform);
            renderers.Add(renderer);
        });
        //renderers.AddRange(scene.renderers);

        //scene.world.Query(visualQuery, (ref VisualComponent visual, ref Transform transform) =>
        //{
        //    if (visual.bindBone == null || !visual.bind.IsAlive() ||
        //        !TryGetComponent<MMDRendererComponent>(visual.bind, out var renderer))
        //    {
        //        return;
        //    }
        //    var v = visual;
        //    var bone = renderer.bones.Find(u => u.Name == v.bindBone);
        //    if (bone == null)
        //        return;
        //    Vector3 pos1 = visual.transform.position;
        //    bone.GetPositionRotation(out var pos, out var rot);
        //    Vector3 pos2 = new Vector3(visual.bindX ? 0 : pos1.X, visual.bindY ? 0 : pos1.Y, visual.bindZ ? 0 : pos1.Z);

        //    Vector3 position = pos + Vector3.Transform(pos1, rot);

        //    position = Vector3.Transform(position, renderer.LocalToWorld);
        //    if (!visual.bindX)
        //        position.X = pos1.X;
        //    if (!visual.bindY)
        //        position.Y = pos1.Y;
        //    if (!visual.bindZ)
        //        position.Z = pos1.Z;
        //    visual.transform = new Transform(position, visual.transform.rotation * (visual.bindRot ? rot : Quaternion.Identity), visual.transform.scale);
        //});

    }

    bool TryGetComponent<T>(in Entity entity, out T value)
    {
        if (entity.Has<T>())
        {
            value = entity.Get<T>();
            return true;
        }
        else
        {
            value = default(T);
            return false;
        }
    }

    #endregion

    public void Dispose()
    {
    }
}
