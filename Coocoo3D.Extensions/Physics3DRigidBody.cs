using BulletSharp;
using System.Numerics;

namespace Coocoo3D.Extensions;

public class Physics3DRigidBody
{
    public RigidBody rigidBody;

    public Matrix4x4 offset;
    public Matrix4x4 invertOffset;

    public Matrix4x4 GetTransform()
    {
        rigidBody.MotionState.GetWorldTransform(out var m);
        return GetMatrix(m);
    }

    Matrix4x4 GetMatrix(BulletSharp.Math.Matrix m)
    {
        return new Matrix4x4(
            (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
            (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
            (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
            (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44);
    }
}
