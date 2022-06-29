using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using System.Numerics;

namespace Coocoo3DGraphics
{
    public class RTInstance
    {
        public RTBottomLevelAccelerationStruct accelerationStruct;
        public string hitGroupName;
        public Matrix4x4 transform = Matrix4x4.Identity;
        public Dictionary<int, object> CBVs;
        public Dictionary<int, object> SRVs;
        public Dictionary<int, object> UAVs;
        public byte instanceMask = 0xff;
    }
}
