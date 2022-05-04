using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Numerics
{
    public static class MathHelper
    {
        public static Vector3 QuaternionToXyz(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Atan2(2.0f * (ei - jk), 1 - 2.0f * (ii + jj));
            result.Y = (float)Math.Asin(2.0f * (ej + ik));
            result.Z = (float)Math.Atan2(2.0f * (ek - ij), 1 - 2.0f * (jj + kk));
            return result;
        }
        public static Vector3 QuaternionToXzy(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Atan2(2.0f * (ei + jk), 1 - 2.0f * (ii + kk));
            result.Y = (float)Math.Atan2(2.0f * (ej + ik), 1 - 2.0f * (jj + kk));
            result.Z = (float)Math.Asin(2.0f * (ek - ij));
            return result;
        }
        public static Vector3 QuaternionToYxz(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Asin(2.0f * (ei - jk));
            result.Y = (float)Math.Atan2(2.0f * (ej + ik), 1 - 2.0f * (ii + jj));
            result.Z = (float)Math.Atan2(2.0f * (ek + ij), 1 - 2.0f * (ii + kk));
            return result;
        }
        public static Vector3 QuaternionToYzx(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Atan2(2.0f * (ei - jk), 1 - 2.0f * (ii + kk));
            result.Y = (float)Math.Atan2(2.0f * (ej - ik), 1 - 2.0f * (jj + kk));
            result.Z = (float)Math.Asin(2.0f * (ek + ij));
            return result;
        }
        public static Vector3 QuaternionToZxy(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Asin(2.0f * (ei + jk));
            result.Y = (float)Math.Atan2(2.0f * (ej - ik), 1 - 2.0f * (ii + jj));
            result.Z = (float)Math.Atan2(2.0f * (ek - ij), 1 - 2.0f * (ii + kk));
            return result;
        }
        public static Vector3 QuaternionToZyx(Quaternion quaternion)
        {
            double ii = quaternion.X * quaternion.X;
            double jj = quaternion.Y * quaternion.Y;
            double kk = quaternion.Z * quaternion.Z;
            double ei = quaternion.W * quaternion.X;
            double ej = quaternion.W * quaternion.Y;
            double ek = quaternion.W * quaternion.Z;
            double ij = quaternion.X * quaternion.Y;
            double ik = quaternion.X * quaternion.Z;
            double jk = quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Atan2(2.0f * (ei + jk), 1 - 2.0f * (ii + jj));
            result.Y = (float)Math.Asin(2.0f * (ej - ik));
            result.Z = (float)Math.Atan2(2.0f * (ek + ij), 1 - 2.0f * (jj + kk));
            return result;
        }

        public static Quaternion XyzToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz - sx * sy * sz);
            result.X = (float)(sx * cy * cz + cx * sy * sz);
            result.Y = (float)(cx * sy * cz - sx * cy * sz);
            result.Z = (float)(sx * sy * cz + cx * cy * sz);
            return result;
        }
        public static Quaternion XzyToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz + sx * sy * sz);
            result.X = (float)(sx * cy * cz - cx * sy * sz);
            result.Y = (float)(cx * sy * cz - sx * cy * sz);
            result.Z = (float)(cx * cy * sz + sx * sy * cz);
            return result;
        }
        public static Quaternion YxzToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz + sx * sy * sz);
            result.X = (float)(sx * cy * cz + cx * sy * sz);
            result.Y = (float)(cx * sy * cz - sx * cy * sz);
            result.Z = (float)(cx * cy * sz - sx * sy * cz);
            return result;
        }
        public static Quaternion YzxToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz - sx * sy * sz);
            result.X = (float)(sx * cy * cz + cx * sy * sz);
            result.Y = (float)(cx * sy * cz + sx * cy * sz);
            result.Z = (float)(cx * cy * sz - sx * sy * cz);
            return result;
        }
        public static Quaternion ZxyToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz - sx * sy * sz);
            result.X = (float)(sx * cy * cz - cx * sy * sz);
            result.Y = (float)(cx * sy * cz + sx * cy * sz);
            result.Z = (float)(cx * cy * sz + sx * sy * cz);
            return result;
        }
        public static Quaternion ZYXToQuaternion(Vector3 euler)
        {
            double cx = Math.Cos(euler.X * 0.5f);
            double sx = Math.Sin(euler.X * 0.5f);
            double cy = Math.Cos(euler.Y * 0.5f);
            double sy = Math.Sin(euler.Y * 0.5f);
            double cz = Math.Cos(euler.Z * 0.5f);
            double sz = Math.Sin(euler.Z * 0.5f);
            Quaternion result;
            result.W = (float)(cx * cy * cz + sx * sy * sz);
            result.X = (float)(sx * cy * cz - cx * sy * sz);
            result.Y = (float)(cx * sy * cz + sx * cy * sz);
            result.Z = (float)(cx * cy * sz - sx * sy * cz);
            return result;
        }
    }
}
