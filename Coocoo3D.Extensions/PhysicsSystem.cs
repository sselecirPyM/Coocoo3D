using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Extensions;

public class _physicsObjects
{
    public List<Physics3DRigidBody> rigidbodies = new();
    public List<Physics3DJoint> joints = new();
}
public class PhysicsSystem
{
    public GameDriverContext gameDriverContext;
    //public World world;
    public Scene scene;

    public Physics3DScene physics3DScene = new();

    public void Initialize()
    {
        physics3DScene.Initialize();
        physics3DScene.SetGravitation(new Vector3(0, -9.801f, 0));
        //world.SubscribeComponentAdded<MMDRendererComponent>(OnAdd);
        //world.SubscribeComponentRemoved<MMDRendererComponent>(OnRemove);
        //world.SubscribeComponentSet<Transform>(OnChange);
        scene.SubscribeComponentAdded<MMDRendererComponent>(OnAdd);
        scene.SubscribeComponentRemoved<MMDRendererComponent>(OnRemove);
        //scene.SubscribeComponentSet<Transform>(OnChange);
    }

    public void OnAdd(in Entity entity, in MMDRendererComponent component)
    {
        component.SetTransform(entity.Get<Transform>());
        var _physicsObjects = AddPhysics(component);
        entity.Add(_physicsObjects);
    }

    public void OnRemove(in Entity entity, in MMDRendererComponent component)
    {
        if(entity.TryGet<_physicsObjects>(out var _PhysicsObjects))
        {
            RemovePhysics(_PhysicsObjects);
        }
    }

    public void OnChange(in Entity entity, in Transform newValue)
    {
        if (entity.TryGet<MMDRendererComponent>(out var renderer) && entity.TryGet<_physicsObjects>(out var _PhysicsObjects))
        {
            renderer.SetTransform(newValue);

            TransformToNew(renderer, _PhysicsObjects.rigidbodies);
            gameDriverContext.RefreshScene = true;
        }
    }

    QueryDescription q2 = new QueryDescription().WithAll<MMDRendererComponent, _physicsObjects>();
    public void Update()
    {

        var resetPhysics = gameDriverContext.RefreshScene;
        var deltaTime = gameDriverContext.DeltaTime;
        if (resetPhysics)
        {
            _ResetPhysics();
            BoneUpdate((float)deltaTime);
            _ResetPhysics();
        }

        BoneUpdate((float)deltaTime);
    }

    void _ResetPhysics()
    {
        scene.world.Query(q2, (Entity entity, ref MMDRendererComponent render, ref _physicsObjects physicsObjects) =>
        {
            render.BoneMorphIKAppend();

            for (int j = 0; j < render.rigidBodyDescs.Count; j++)
            {
                var desc = render.rigidBodyDescs[j];
                if (desc.Type == 0)
                    continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1)
                    continue;

                Matrix4x4 matrix = desc.transform * render.bones[index].GeneratedTransform * render.LocalToWorld;
                physics3DScene.ResetRigidBody(physicsObjects.rigidbodies[j], matrix);
            }
        });
        //physics3DScene.Simulation(1 / 60.0);
    }

    _physicsObjects AddPhysics(MMDRendererComponent r)
    {
        var _physicsObjects = new _physicsObjects();
        var rigidbodies = _physicsObjects.rigidbodies;
        var joints = _physicsObjects.joints;
        for (int j = 0; j < r.rigidBodyDescs.Count; j++)
        {
            rigidbodies.Add(new Physics3DRigidBody());
            var desc = r.rigidBodyDescs[j];
            physics3DScene.AddRigidBody(rigidbodies[j], desc);
        }
        for (int j = 0; j < r.jointDescs.Count; j++)
        {
            joints.Add(new Physics3DJoint());
            var desc = r.jointDescs[j];
            physics3DScene.AddJoint(joints[j], rigidbodies[desc.AssociatedRigidBodyIndex1], rigidbodies[desc.AssociatedRigidBodyIndex2], desc);
        }
        return _physicsObjects;
    }
    void RemovePhysics(_physicsObjects _physicsObjects)
    {
        var joints = _physicsObjects.joints;
        for (int j = 0; j < joints.Count; j++)
        {
            physics3DScene.RemoveJoint(joints[j]);
        }
        joints.Clear();
        var rigidbodies = _physicsObjects.rigidbodies;
        for (int j = 0; j < rigidbodies.Count; j++)
        {
            physics3DScene.RemoveRigidBody(rigidbodies[j]);
        }
        rigidbodies.Clear();
    }
    void PrePhysicsSync(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
    {
        for (int i = 0; i < r.rigidBodyDescs.Count; i++)
        {
            var desc = r.rigidBodyDescs[i];
            if (desc.Type != 0)
                continue;
            int index = desc.AssociatedBoneIndex;
            if (index == -1)
                continue;

            Matrix4x4 matrix = desc.transform * r.bones[index].GeneratedTransform * r.LocalToWorld;
            physics3DScene.MoveRigidBody(rigidbodies[i], matrix);
        }
    }

    void PhysicsSyncBack(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
    {
        for (int i = 0; i < r.rigidBodyDescs.Count; i++)
        {
            var desc = r.rigidBodyDescs[i];
            if (desc.Type == 0)
                continue;
            int index = desc.AssociatedBoneIndex;
            if (index == -1)
                continue;
            var bone = r.bones[index];
            bone.GeneratedTransform = desc.invertTransform *
                rigidbodies[i].GetTransform() * r.WorldToLocal;
        }
    }

    void BoneUpdate(float deltaTime)
    {
        float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
        scene.world.Query(q2, (Entity entity, ref MMDRendererComponent render, ref _physicsObjects physicsObjects) =>
        {
            PrePhysicsSync(render, physicsObjects.rigidbodies);
        });
        physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
        scene.world.Query(q2, (Entity entity, ref MMDRendererComponent render, ref _physicsObjects physicsObjects) =>
        {
            PhysicsSyncBack(render, physicsObjects.rigidbodies);
        });
    }

    void TransformToNew(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
    {
        for (int i = 0; i < r.rigidBodyDescs.Count; i++)
        {
            var desc = r.rigidBodyDescs[i];
            if (desc.Type != RigidBodyType.Kinematic)
                continue;

            var bone = r.bones[desc.AssociatedBoneIndex];
            Matrix4x4 matrix = desc.transform * bone.GeneratedTransform * r.LocalToWorld;
            physics3DScene.MoveRigidBody(rigidbodies[i], matrix);
        }
    }

    public void Dispose()
    {
        scene.world.Query(q2, (Entity entity, ref MMDRendererComponent render, ref _physicsObjects physicsObjects) =>
        {
            foreach (var joint in physicsObjects.joints)
            {
                physics3DScene.RemoveJoint(joint);
            }
            foreach (var rigidBody in physicsObjects.rigidbodies)
            {
                physics3DScene.RemoveRigidBody(rigidBody);
            }
        });
        physics3DScene.Dispose();
    }
}
