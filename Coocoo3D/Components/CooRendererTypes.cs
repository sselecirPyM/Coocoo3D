using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public enum BoneFlags
    {
        ChildUseId = 1,
        Rotatable = 2,
        Movable = 4,
        Visible = 8,
        Controllable = 16,
        HasIK = 32,
        AcquireRotate = 256,
        AcquireTranslate = 512,
        RotAxisFixed = 1024,
        UseLocalAxis = 2048,
        PostPhysics = 4096,
        ReceiveTransform = 8192
    }
    public enum MorphType
    {
        Group = 0,
        Vertex = 1,
        Bone = 2,
        UV = 3,
        ExtUV1 = 4,
        ExtUV2 = 5,
        ExtUV3 = 6,
        ExtUV4 = 7,
        Material = 8
    }
    public struct MorphVertexDesc
    {
        public int VertexIndex;
        public Vector3 Offset;
    }
    public struct MorphBoneDesc
    {
        public int BoneIndex;
        public Vector3 Translation;
        public Quaternion Rotation;
    }
    public enum IKTransformOrder
    {
        Yzx = 0,
        Zxy = 1,
        Xyz = 2,
    }

    public enum AxisFixType
    {
        FixNone,
        FixX,
        FixY,
        FixZ,
        FixAll
    }

    public enum RigidBodyType
    {
        Kinematic = 0,
        Physics = 1,
        PhysicsStrict = 2,
        PhysicsGhost = 3
    }

    public enum RigidBodyShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    public struct RigidBodyDesc
    {
        public int AssociatedBoneIndex;
        public byte CollisionGroup;
        public ushort CollisionMask;
        public RigidBodyShape Shape;
        public Vector3 Dimemsions;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Mass;
        public float LinearDamping;
        public float AngularDamping;
        public float Restitution;
        public float Friction;
        public RigidBodyType Type;
    }

    public struct JointDesc
    {
        public byte Type;
        public int AssociatedRigidBodyIndex1;
        public int AssociatedRigidBodyIndex2;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 PositionMinimum;
        public Vector3 PositionMaximum;
        public Vector3 RotationMinimum;
        public Vector3 RotationMaximum;
        public Vector3 PositionSpring;
        public Vector3 RotationSpring;
    }
    [Flags]
    public enum DrawFlag
    {
        None = 0,
        DrawDoubleFace = 1,
        DrawGroundShadow = 2,
        CastSelfShadow = 4,
        DrawSelfShadow = 8,
        DrawEdge = 16,
    }
}
