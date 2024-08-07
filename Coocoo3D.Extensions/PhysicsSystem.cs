using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace Coocoo3D.Extensions;

public class _physicsObjects
{
    public List<Physics3DRigidBody> rigidbodies = new();
    public List<Physics3DJoint> joints = new();
}
[Export(typeof(ISceneExtension))]
public class PhysicsSystem : ISceneExtension, IDisposable
{
    public GameDriverContext gameDriverContext;
    public World world;
    EntitySet set;

    public Physics3DScene physics3DScene = new();

    public Dictionary<MMDRendererComponent, _physicsObjects> physicsObjects = new();

    List<MMDRendererComponent> rendererComponents = new();

    public override void Initialize()
    {
        physics3DScene.Initialize();
        physics3DScene.SetGravitation(new Vector3(0, -9.801f, 0));
        world.SubscribeComponentAdded<MMDRendererComponent>(OnAdd);
        world.SubscribeComponentRemoved<MMDRendererComponent>(OnRemove);
        world.SubscribeComponentChanged<Transform>(OnChange);
        set = world.GetEntities().With<MMDRendererComponent>().AsSet();
    }

    public void OnAdd(in Entity entity, in MMDRendererComponent component)
    {
        component.SetTransform(entity.Get<Transform>());
        physicsObjects[component] = AddPhysics(component);
    }

    public void OnRemove(in Entity entity, in MMDRendererComponent component)
    {
        if (physicsObjects.Remove(component, out var phyObj))
            RemovePhysics(phyObj);
    }

    public void OnChange(in Entity entity, in Transform oldValue, in Transform newValue)
    {
        if (entity.Has<MMDRendererComponent>())
        {
            var renderer = entity.Get<MMDRendererComponent>();
            renderer.SetTransform(newValue);

            var phyObj = GetPhysics(renderer);
            TransformToNew(renderer, phyObj.rigidbodies);
            gameDriverContext.RefreshScene = true;
        }
    }

    public override void Update()
    {
        foreach (var gameObject in set.GetEntities())
        {
            var render = gameObject.Get<MMDRendererComponent>();
            rendererComponents.Add(render);
        }
        var resetPhysics = gameDriverContext.RefreshScene;
        var deltaTime = gameDriverContext.DeltaTime;
        if (resetPhysics)
        {
            _ResetPhysics(rendererComponents);
            BoneUpdate((float)deltaTime, rendererComponents);
            _ResetPhysics(rendererComponents);
        }

        BoneUpdate((float)deltaTime, rendererComponents);
        rendererComponents.Clear();
    }

    void _ResetPhysics(IReadOnlyList<MMDRendererComponent> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            r.BoneMorphIKAppend();
            var phyO = GetPhysics(r);

            for (int j = 0; j < r.rigidBodyDescs.Count; j++)
            {
                var desc = r.rigidBodyDescs[j];
                if (desc.Type == 0)
                    continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1)
                    continue;

                Matrix4x4 matrix = desc.transform * r.bones[index].GeneratedTransform * r.LocalToWorld;
                physics3DScene.ResetRigidBody(phyO.rigidbodies[j], matrix);
            }
        }
        physics3DScene.Simulation(1 / 60.0);
    }

    _physicsObjects GetPhysics(MMDRendererComponent r)
    {
        return physicsObjects[r];
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
            bone._generatedTransform = desc.invertTransform *
                rigidbodies[i].GetTransform() * r.WorldToLocal;
            Matrix4x4 invParentMatrix;
            if (bone.ParentIndex >= 0)
                Matrix4x4.Invert(r.bones[bone.ParentIndex]._generatedTransform, out invParentMatrix);
            else
                invParentMatrix = Matrix4x4.Identity;

            var localMatrix = invParentMatrix * bone._generatedTransform;
            Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var translation);
            bone.translation = translation;
            bone.rotation = rotation;
        }
        r.UpdateMatrices(r.PhysicsUpdateMatrixIndice);

        r.UpdateAppendBones();
    }

    void BoneUpdate(float deltaTime, IReadOnlyList<MMDRendererComponent> renderers)
    {
        float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            var _PhysicsObjects = GetPhysics(r);
            PrePhysicsSync(r, _PhysicsObjects.rigidbodies);
        }
        physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            physicsObjects.TryGetValue(r, out var _PhysicsObjects);
            PhysicsSyncBack(r, _PhysicsObjects.rigidbodies);
        }
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
        foreach (var physicsObject in physicsObjects.Values)
        {
            foreach (var joint in physicsObject.joints)
            {
                physics3DScene.RemoveJoint(joint);
            }
            foreach (var rigidBody in physicsObject.rigidbodies)
            {
                physics3DScene.RemoveRigidBody(rigidBody);
            }
        }
        physics3DScene.Dispose();
    }
}
