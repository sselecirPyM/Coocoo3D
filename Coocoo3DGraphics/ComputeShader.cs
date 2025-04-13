using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Shader;

namespace Coocoo3DGraphics;

public class ComputeShader : IDisposable
{
    public byte[] data;
    ID3D12ShaderReflection reflection;

    RootSignature rootSignature1;
    ID3D12PipelineState computePipeline;

    public ComputeShader(byte[] data, ID3D12ShaderReflection reflection)
    {
        this.data = data.AsSpan().ToArray();
        this.reflection = reflection;
    }

    internal bool TryGetPipelineState1(ID3D12Device device, out RootSignature rootSignature, out ID3D12PipelineState pipelineState)
    {
        CreateRootSignature();
        var rootSignature2 = this.rootSignature1.GetRootSignature(device);
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
        rootSignature1 = new RootSignature();
        foreach (var res in reflection.BoundResources)
        {
            switch (res.Type)
            {
                case ShaderInputType.Texture:
                case ShaderInputType.Structured:
                    rootSignature1.srv[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                    DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                case ShaderInputType.ConstantBuffer:
                    rootSignature1.cbv[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    break;
                case ShaderInputType.Sampler:
                    samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                            0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                    break;
                case ShaderInputType.UnorderedAccessViewRWTyped:
                case ShaderInputType.UnorderedAccessViewRWStructured:
                    rootSignature1.uav[res.BindPoint] = parameters.Count;
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                            DescriptorRangeType.UnorderedAccessView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                default:
                    break;
            }
        }
        var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), samplers.ToArray());
        rootSignature1.FromDesc(rootSignatureDescription1);
    }

    public void Dispose()
    {
        computePipeline?.Release();
        computePipeline = null;
        rootSignature1.Dispose();
        rootSignature1 = null;
        reflection?.Release();
        reflection = null;
    }
}
