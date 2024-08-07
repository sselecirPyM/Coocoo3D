using System;
using System.Numerics;

namespace Coocoo3D.Components;

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
    public Vector3 Dimensions;
    public float Mass;
    public float LinearDamping;
    public float AngularDamping;
    public float Restitution;
    public float Friction;
    public RigidBodyType Type;

    public Matrix4x4 transform;
    public Matrix4x4 invertTransform;
}

public struct JointDesc
{
    public byte Type;
    public int AssociatedRigidBodyIndex1;
    public int AssociatedRigidBodyIndex2;
    public Vector3 LinearMinimum;
    public Vector3 LinearMaximum;
    public Vector3 AngularMinimum;
    public Vector3 AngularMaximum;
    public Vector3 LinearSpring;
    public Vector3 AngularSpring;
    public Matrix4x4 transform;
    public Matrix4x4 invertTransform;
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
