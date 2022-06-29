using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class GPUBuffer : IDisposable
    {
        public ID3D12Resource resource;
        public string Name;
        public ResourceStates resourceStates;
        public int size;

        public void StateChange(ID3D12GraphicsCommandList commandList, ResourceStates states)
        {
            if (states != resourceStates)
            {
                commandList.ResourceBarrierTransition(resource, resourceStates, states);
                resourceStates = states;
            }
            else if (states == ResourceStates.UnorderedAccess)
            {
                commandList.ResourceBarrierUnorderedAccessView(resource);
            }
        }
        public void Dispose()
        {
            resource?.Release();
            resource = null;
        }
    }
}
