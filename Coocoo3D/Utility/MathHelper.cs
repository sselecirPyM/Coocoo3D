using System;
using System.Numerics;

namespace Coocoo3D.Utility
{
    public static class MathHelper
    {
        public static Vector3 QuaternionToXyz(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Atan2(2.0f * (ei - jk), 1 - 2.0f * (ii + jj));
            result.Y = MathF.Asin(2.0f * (ej + ik));
            result.Z = MathF.Atan2(2.0f * (ek - ij), 1 - 2.0f * (jj + kk));
            return result;
        }
        public static Vector3 QuaternionToXzy(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Atan2(2.0f * (ei + jk), 1 - 2.0f * (ii + kk));
            result.Y = MathF.Atan2(2.0f * (ej + ik), 1 - 2.0f * (jj + kk));
            result.Z = MathF.Asin(2.0f * (ek - ij));
            return result;
        }
        public static Vector3 QuaternionToYxz(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Asin(2.0f * (ei - jk));
            result.Y = MathF.Atan2(2.0f * (ej + ik), 1 - 2.0f * (ii + jj));
            result.Z = MathF.Atan2(2.0f * (ek + ij), 1 - 2.0f * (ii + kk));
            return result;
        }
        public static Vector3 QuaternionToYzx(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Atan2(2.0f * (ei - jk), 1 - 2.0f * (ii + kk));
            result.Y = MathF.Atan2(2.0f * (ej - ik), 1 - 2.0f * (jj + kk));
            result.Z = MathF.Asin(2.0f * (ek + ij));
            return result;
        }
        public static Vector3 QuaternionToZxy(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Asin(2.0f * (ei + jk));
            result.Y = MathF.Atan2(2.0f * (ej - ik), 1 - 2.0f * (ii + jj));
            result.Z = MathF.Atan2(2.0f * (ek - ij), 1 - 2.0f * (ii + kk));
            return result;
        }
        public static Vector3 QuaternionToZyx(Quaternion quaternion)
        {
            float ii = quaternion.X * quaternion.X;
            float jj = quaternion.Y * quaternion.Y;
            float kk = quaternion.Z * quaternion.Z;
            float ei = quaternion.W * quaternion.X;
            float ej = quaternion.W * quaternion.Y;
            float ek = quaternion.W * quaternion.Z;
            float ij = quaternion.X * quaternion.Y;
            float ik = quaternion.X * quaternion.Z;
            float jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = MathF.Atan2(2.0f * (ei + jk), 1 - 2.0f * (ii + jj));
            result.Y = MathF.Asin(2.0f * (ej - ik));
            result.Z = MathF.Atan2(2.0f * (ek + ij), 1 - 2.0f * (jj + kk));
            return result;
        }

        public static Quaternion XyzToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz - sx * sy * sz);
            result.X = (sx * cy * cz + cx * sy * sz);
            result.Y = (cx * sy * cz - sx * cy * sz);
            result.Z = (sx * sy * cz + cx * cy * sz);
            return result;
        }
        public static Quaternion XzyToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz + sx * sy * sz);
            result.X = (sx * cy * cz - cx * sy * sz);
            result.Y = (cx * sy * cz - sx * cy * sz);
            result.Z = (cx * cy * sz + sx * sy * cz);
            return result;
        }
        public static Quaternion YxzToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz + sx * sy * sz);
            result.X = (sx * cy * cz + cx * sy * sz);
            result.Y = (cx * sy * cz - sx * cy * sz);
            result.Z = (cx * cy * sz - sx * sy * cz);
            return result;
        }
        public static Quaternion YzxToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz - sx * sy * sz);
            result.X = (sx * cy * cz + cx * sy * sz);
            result.Y = (cx * sy * cz + sx * cy * sz);
            result.Z = (cx * cy * sz - sx * sy * cz);
            return result;
        }
        public static Quaternion ZxyToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz - sx * sy * sz);
            result.X = (sx * cy * cz - cx * sy * sz);
            result.Y = (cx * sy * cz + sx * cy * sz);
            result.Z = (cx * cy * sz + sx * sy * cz);
            return result;
        }
        public static Quaternion ZYXToQuaternion(Vector3 euler)
        {
            float cx = MathF.Cos(euler.X * 0.5f);
            float sx = MathF.Sin(euler.X * 0.5f);
            float cy = MathF.Cos(euler.Y * 0.5f);
            float sy = MathF.Sin(euler.Y * 0.5f);
            float cz = MathF.Cos(euler.Z * 0.5f);
            float sz = MathF.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (cx * cy * cz + sx * sy * sz);
            result.X = (sx * cy * cz - cx * sy * sz);
            result.Y = (cx * sy * cz + sx * cy * sz);
            result.Z = (cx * cy * sz - sx * sy * cz);
            return result;
        }
    }
}
