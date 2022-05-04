using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class LightingComponent : Component
    {
        public LightingType LightingType;
        public Vector3 Color;
        public float Range;

        public DirectionalLightData GetDirectionalLightData(Quaternion rotation)
        {
            return new DirectionalLightData
            {
                Rotation = rotation,
                Direction = Vector3.Transform(-Vector3.UnitZ, rotation),
                Color = Color,
            };
        }

        public PointLightData GetPointLightData(Vector3 position)
        {
            return new PointLightData
            {
                Position = position,
                Color = Color,
                Range = Math.Max(Range, 1e-4f),
            };
        }
    }
}
