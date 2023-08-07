using System;
using System.Numerics;

namespace Coocoo3D.Present;

public struct Transform : IEquatable<Transform>
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public Transform(Vector3 position, Quaternion rotation)
    {
        this.position = position;
        this.rotation = rotation;
        this.scale = Vector3.One;
    }

    public Transform(Vector3 position, Quaternion rotation,Vector3 scale)
    {
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
    }

    public override bool Equals(object obj)
    {
        return obj is Transform transform && Equals(transform);
    }

    public bool Equals(Transform other)
    {
        return position.Equals(other.position) &&
               rotation.Equals(other.rotation) &&
               scale.Equals(other.scale);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(position, rotation);
    }

    public static bool operator ==(Transform left, Transform right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Transform left, Transform right)
    {
        return !(left == right);
    }

    public Matrix4x4 GetMatrix()
    {
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }
}
