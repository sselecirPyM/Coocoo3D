using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class MatrixExt
    {
        public static Matrix4x4 Transform(Vector3 position, Quaternion rotation)
        {
            return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        }
        public static Matrix4x4 InverseTransform(Vector3 position, Quaternion rotation)
        {
            return Matrix4x4.CreateTranslation(-position) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotation));
        }
    }
}
