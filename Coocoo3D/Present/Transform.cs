using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public struct Transform : IEquatable<Transform>
    {
        public Vector3 position;
        public Quaternion rotation;

        public Transform(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        public override bool Equals(object obj)
        {
            return obj is Transform transform && Equals(transform);
        }

        public bool Equals(Transform other)
        {
            return position.Equals(other.position) &&
                   rotation.Equals(other.rotation);
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
            return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        }
    }
}
