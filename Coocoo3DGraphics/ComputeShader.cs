using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Shader;

namespace Coocoo3DGraphics;

public class ComputeShader : IDisposable
{
    public byte[] data;
    public Dictionary<ID3D12RootSignature, ID3D12PipelineState> computeShaders = new Dictionary<ID3D12RootSignature, ID3D12PipelineState>();
    ID3D12ShaderReflection reflection;

    RootSignature rootSignature1;
    ID3D12PipelineState computePipeline;

    public void Initialize(byte[] data, ID3D12ShaderReflection reflection)
    {
        this.data = data.AsSpan().ToArray();
        this.reflection = reflection;
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

    internal bool TryGetPipelineState1(GraphicsDevice graphicsDevice, out RootSignature rootSignature, out ID3D12PipelineState pipelineState)
    {
        ID3D12Device device = graphicsDevice.device;
        CreateRootSignature();
        var rootSignature2 = this.rootSignature1.GetRootSignature(graphicsDevice);
        rootSignature = rootSignature1;
        if (computePipeline == null)
        {
            var desc = new ComputePipelineStateDescription
            {
                ComputeShader = data,
                RootSignature = rootSignature2
            };
            if (device.CreateComputePipelineState(desc, out pipelineState).Failure)
            {
                return false;
            }
            else
            {
                computePipeline = pipelineState;
            }
        }
        pipelineState = computePipeline;
        return true;
    }

    void CreateRootSignature()
    {
        if (rootSignature1 != null)
            return;
        var parameters = new List<RootParameter1>();
        var samplers = new List<StaticSamplerDescription>();
        foreach (var res in reflection.BoundResources)
        {
            switch (res.Type)
            {
                case ShaderInputType.Texture:
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                    DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                case ShaderInputType.ConstantBuffer:
                    parameters.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    break;
                case ShaderInputType.Sampler:
                    samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                            0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                    break;
                case ShaderInputType.UnorderedAccessViewRWTyped:
                case ShaderInputType.UnorderedAccessViewRWStructured:
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                            DescriptorRangeType.UnorderedAccessView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                default:
                    break;
            }
        }
        var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), samplers.ToArray());
        rootSignature1 = new RootSignature();
        rootSignature1.FromDesc(rootSignatureDescription1);
    }

    public void Dispose()
    {
        rootSignature1.Dispose();
        rootSignature1 = null;
        reflection?.Release();
        reflection = null;
        foreach (var shader in computeShaders)
        {
            shader.Value.Release();
        }
        computeShaders.Clear();
    }
}
