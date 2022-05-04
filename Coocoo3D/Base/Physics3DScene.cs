using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using BulletSharp;
using Coocoo3D.Utility;

namespace Coocoo3D.Base
{
    public class Physics3DScene
    {
        DefaultCollisionConfiguration defaultCollisionConfiguration = new DefaultCollisionConfiguration();
        DbvtBroadphase broadphase = new DbvtBroadphase();
        SequentialImpulseConstraintSolver sequentialImpulseConstraintSolver = new SequentialImpulseConstraintSolver();
        Dispatcher dispatcher;
        DiscreteDynamicsWorld world;
        public void Initialize()
        {
            dispatcher = new CollisionDispatcher(defaultCollisionConfiguration);
            world = new DiscreteDynamicsWorld(dispatcher, broadphase, sequentialImpulseConstraintSolver, defaultCollisionConfiguration);
            BulletSharp.Math.Vector3 gravity = new BulletSharp.Math.Vector3(0, -9.81f, 0);
            world.SetGravity(ref gravity);
        }
        public void SetGravitation(Vector3 g)
        {
            BulletSharp.Math.Vector3 gravity = GetVector3(g);
            world.SetGravity(ref gravity);
        }
        public void AddRigidBody(Physics3DRigidBody rb, Components.RigidBodyDesc desc)
        {
            MotionState motionState;
            Matrix4x4 mat = MatrixExt.Transform(desc.Position, desc.Rotation);
            rb.defaultPosition = desc.Position;
            rb.defaultRotation = desc.Rotation;

            motionState = new DefaultMotionState(GetMatrix(mat));
            CollisionShape collisionShape;
            switch (desc.Shape)
            {
                case Components.RigidBodyShape.Sphere:
                    collisionShape = new SphereShape(desc.Dimemsions.X);
                    break;
                case Components.RigidBodyShape.Capsule:
                    collisionShape = new CapsuleShape(desc.Dimemsions.X, desc.Dimemsions.Y);
                    break;
                case Components.RigidBodyShape.Box:
                default:
                    collisionShape = new BoxShape(GetVector3(desc.Dimemsions));
                    break;
            }
            float mass = desc.Mass;
            BulletSharp.Math.Vector3 localInertia = new BulletSharp.Math.Vector3();
            if (desc.Type == 0) mass = 0;
            else
            {
                collisionShape.CalculateLocalInertia(mass, out localInertia);
            }
            var rigidbodyInfo = new RigidBodyConstructionInfo(mass, motionState, collisionShape, localInertia);
            rigidbodyInfo.Friction = desc.Friction;
            rigidbodyInfo.LinearDamping = desc.LinearDamping;
            rigidbodyInfo.AngularDamping = desc.AngularDamping;
            rigidbodyInfo.Restitution = desc.Restitution;

            rb.rigidBody = new RigidBody(rigidbodyInfo);
            rb.rigidBody.ActivationState = ActivationState.DisableDeactivation;
            rb.rigidBody.SetSleepingThresholds(0, 0);
            if (desc.Type == Components.RigidBodyType.Kinematic)
            {
                rb.rigidBody.CollisionFlags |= CollisionFlags.KinematicObject;
            }
            world.AddRigidBody(rb.rigidBody, 1 << desc.CollisionGroup, desc.CollisionMask);
        }

        public void AddJoint(Physics3DJoint joint, Physics3DRigidBody r1, Physics3DRigidBody r2, Components.JointDesc desc)
        {

            var t0 = MatrixExt.Transform(desc.Position, ToQuaternion(desc.Rotation));
            Matrix4x4.Invert(t0, out var res);
            Matrix4x4.Invert(MatrixExt.Transform(r1.defaultPosition, r1.defaultRotation), out var t1);
            Matrix4x4.Invert(MatrixExt.Transform(r2.defaultPosition, r2.defaultRotation), out var t2);
            t1 = t0 * t1;
            t2 = t0 * t2;

            var j = new Generic6DofSpringConstraint(r1.rigidBody, r2.rigidBody, GetMatrix(t1), GetMatrix(t2), true);
            joint.constraint = j;
            j.LinearLowerLimit = GetVector3(desc.PositionMinimum);
            j.LinearUpperLimit = GetVector3(desc.PositionMaximum);
            j.AngularLowerLimit = GetVector3(desc.RotationMinimum);
            j.AngularUpperLimit = GetVector3(desc.RotationMaximum);

            S(0, desc.PositionSpring.X);
            S(1, desc.PositionSpring.Y);
            S(2, desc.PositionSpring.Z);
            S(3, desc.RotationSpring.X);
            S(4, desc.RotationSpring.Y);
            S(5, desc.RotationSpring.Z);
            void S(int index, float f)
            {
                if (f != 0.0f)
                {
                    j.EnableSpring(index, true);
                    j.SetStiffness(index, f);
                }
                else
                {
                    j.EnableSpring(index, false);
                }
            }

            world.AddConstraint(joint.constraint);
        }

        public void Simulation(double time)
        {
            world.StepSimulation(time);
        }

        public void ResetRigidBody(Physics3DRigidBody rb, Vector3 position, Quaternion rotation)
        {
            var worldTransform = GetMatrix(MatrixExt.Transform(position, rotation));
            var rigidBody = rb.rigidBody;
            rigidBody.MotionState.SetWorldTransform(ref worldTransform);
            rigidBody.CenterOfMassTransform = worldTransform;
            rigidBody.InterpolationWorldTransform = worldTransform;
            rigidBody.InterpolationWorldTransform = worldTransform;
            rigidBody.AngularVelocity = new BulletSharp.Math.Vector3();
            rigidBody.LinearVelocity = new BulletSharp.Math.Vector3();
            rigidBody.InterpolationAngularVelocity = new BulletSharp.Math.Vector3();
            rigidBody.InterpolationLinearVelocity = new BulletSharp.Math.Vector3();
            rigidBody.ClearForces();
        }

        public void ResetRigidBody(Physics3DRigidBody rb, Matrix4x4 mat)
        {
            var worldTransform = GetMatrix(mat);
            var rigidBody = rb.rigidBody;
            rigidBody.MotionState.SetWorldTransform(ref worldTransform);
            rigidBody.CenterOfMassTransform = worldTransform;
            rigidBody.InterpolationWorldTransform = worldTransform;
            rigidBody.InterpolationWorldTransform = worldTransform;
            rigidBody.AngularVelocity = new BulletSharp.Math.Vector3();
            rigidBody.LinearVelocity = new BulletSharp.Math.Vector3();
            rigidBody.InterpolationAngularVelocity = new BulletSharp.Math.Vector3();
            rigidBody.InterpolationLinearVelocity = new BulletSharp.Math.Vector3();
            rigidBody.ClearForces();
        }

        public void MoveRigidBody(Physics3DRigidBody rb, Matrix4x4 mat)
        {
            rb.rigidBody.MotionState.WorldTransform = GetMatrix(mat);
        }

        public void RemoveRigidBody(Physics3DRigidBody rb)
        {
            world.RemoveRigidBody(rb.rigidBody);
            rb.rigidBody.Dispose();
        }

        public void RemoveJoint(Physics3DJoint joint)
        {
            world.RemoveConstraint(joint.constraint);
            joint.constraint.Dispose();
        }

        public BulletSharp.Math.Vector3 GetVector3(Vector3 v)
        {
            return new BulletSharp.Math.Vector3(v.X, v.Y, v.Z);
        }

        public BulletSharp.Math.Matrix GetMatrix(Matrix4x4 mat)
        {
            BulletSharp.Math.Matrix m = new BulletSharp.Math.Matrix();
            m.M11 = mat.M11;
            m.M12 = mat.M12;
            m.M13 = mat.M13;
            m.M14 = mat.M14;
            m.M21 = mat.M21;
            m.M22 = mat.M22;
            m.M23 = mat.M23;
            m.M24 = mat.M24;
            m.M31 = mat.M31;
            m.M32 = mat.M32;
            m.M33 = mat.M33;
            m.M34 = mat.M34;
            m.M41 = mat.M41;
            m.M42 = mat.M42;
            m.M43 = mat.M43;
            m.M44 = mat.M44;
            return m;
        }

        public static Quaternion ToQuaternion(Vector3 angle)
        {
            return Quaternion.CreateFromYawPitchRoll(angle.Y, angle.X, angle.Z);
        }
    }
}
