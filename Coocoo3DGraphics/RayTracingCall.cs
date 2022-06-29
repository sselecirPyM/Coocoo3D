using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RayTracingCall
    {
        public string rayGenShader;
        public string[] missShaders;

        public Dictionary<int, object> CBVs;
        public Dictionary<int, object> SRVs;
        public Dictionary<int, object> UAVs;
        public Dictionary<int, int> srvFlags = new Dictionary<int, int>();

        public RTTopLevelAcclerationStruct tpas;

    }
}
