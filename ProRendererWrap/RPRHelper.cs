using FireRender.AMD.RenderEngine.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ProRendererWrap
{
    internal static class RPRHelper
    {
        public static void Check(Rpr.Status status)
        {
            if (status != Rpr.Status.SUCCESS) throw new Exception(status.ToString());
            //return status == Rpr.Status.SUCCESS;
        }

        public static void MatrixToFloatArray(Matrix4x4 matrix, Span<float> data)
        {
            data[0] = matrix.M11;
            data[1] = matrix.M12;
            data[2] = matrix.M13;
            data[3] = matrix.M14;
            data[4] = matrix.M21;
            data[5] = matrix.M22;
            data[6] = matrix.M23;
            data[7] = matrix.M24;
            data[8] = matrix.M31;
            data[9] = matrix.M32;
            data[10] = matrix.M33;
            data[11] = matrix.M34;
            data[12] = matrix.M41;
            data[13] = matrix.M42;
            data[14] = matrix.M43;
            data[15] = matrix.M44;
        }

        public const string dllName = "RadeonProRender64";
    }
}
