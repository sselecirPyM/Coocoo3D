using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RTPSO : IDisposable
    {
        public RayTracingShaderDescription[] rayGenShaders;
        public RayTracingShaderDescription[] hitGroups;
        public RayTracingShaderDescription[] missShaders;
        public string[] exports;
        public byte[] datas;
        public ResourceAccessType[] shaderAccessTypes;
        public ResourceAccessType[] localShaderAccessTypes;
        public ID3D12StateObject so;
        public RootSignature globalRootSignature;
        public RootSignature localRootSignature;
        public int localSize = 32;

        public void Dispose()
        {
            so?.Release();
            so = null;
            globalRootSignature?.Dispose();
            globalRootSignature = null;
            localRootSignature?.Dispose();
            localRootSignature = null;
        }
    }
}
