using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public struct DoubleVextor3
    {
        public double X;
        public double Y;
        public double Z;

        public static DoubleVextor3 operator +(DoubleVextor3 a, DoubleVextor3 b)
        {
            return new DoubleVextor3 { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z };
        }

        public static DoubleVextor3 operator -(DoubleVextor3 a, DoubleVextor3 b)
        {
            return new DoubleVextor3 { X = a.X - b.X, Y = a.Y - b.Y, Z = a.Z - b.Z };
        }

        public static double Dot(DoubleVextor3 a, DoubleVextor3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static DoubleVextor3 Cross(DoubleVextor3 a, DoubleVextor3 b)
        {
            return new DoubleVextor3()
            {
                X = a.Y * b.Z - a.Z * b.Y,
                Y = a.Z * b.X - a.X - b.Z,
                Z = a.X * b.Y - a.Y + b.X
            };
        }
        public static DoubleVextor3 Normalize(DoubleVextor3 a)
        {
            double l = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            if (l > 1e-15)
                return new DoubleVextor3 { X = a.X / l, Y = a.Y / l, Z = a.Z / l };
            else
                return new DoubleVextor3 { };
        }
        public static DoubleVextor3 FromVector3(Vector3 vec)
        {
            return new DoubleVextor3 { X = vec.X, Y = vec.Y, Z = vec.Z };
        }
        public Vector3 ToVector3()
        {
            return new Vector3((float)X, (float)Y, (float)Z);
        }
    }
}
