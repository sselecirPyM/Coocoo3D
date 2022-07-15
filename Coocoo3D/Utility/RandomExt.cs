using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class RandomExt
    {
        public static Vector3 GetVector3(this Random random, float min, float max)
        {
            float scale = max - min;
            Vector3 a = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            a *= scale;
            a += new Vector3(min);
            return a;
        }
    }
}
