using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    internal struct PointLightData
    {
        public Vector3 Position;
        public int unuse;
        public Vector3 Color;
        public float Range;
    }
}
