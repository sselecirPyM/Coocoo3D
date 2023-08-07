using System;
using System.Collections.Generic;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class ComputeShader : IDisposable
{
    public byte[] data;
    public Dictionary<ID3D12RootSignature, ID3D12PipelineState> computeShaders = new Dictionary<ID3D12RootSignature, ID3D12PipelineState>();
    public void Initialize(byte[] data)
    {
        this.data = new byte[data.Length];
        Array.Copy(data, this.data, data.Length);
    }

    internal bool TryGetPipelineState(ID3D12Device device, ID3D12RootSignature rootSignature, out ID3D12PipelineState pipelineState)
    {
        if (!computeShaders.TryGetValue(rootSignature, out pipelineState))
        {
            var desc = new ComputePipelineStateDescription
            {
                ComputeShader = data,
                RootSignature = rootSignature
            };
            if (device.CreateComputePipelineState(desc, out pipelineState).Failure)
            {
                return false;
            }
            computeShaders[rootSignature] = pipelineState;
        }
        return true;
    }

    public void Dispose()
    {
        foreach (var shader in computeShaders)
        {
            shader.Value.Release();
        }
        computeShaders.Clear();
    }
}
