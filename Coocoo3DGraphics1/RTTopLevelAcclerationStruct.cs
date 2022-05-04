using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RTTopLevelAcclerationStruct : IDisposable
    {
        public List<RTInstance> instances;
        public RaytracingInstanceDescription raytracingInstanceDescription;
        public bool initialized;
        public ID3D12Resource resource;

        public void Dispose()
        {
            resource?.Release();
            resource = null;
        }
    }
}
