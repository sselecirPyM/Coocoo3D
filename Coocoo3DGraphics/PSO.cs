using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Shader;
using Vortice.DXGI;

namespace Coocoo3DGraphics;

public enum BlendState
{
    None = 0,
    Alpha = 1,
    Add = 2,
    PreserveAlpha = 3,
};
public enum InputLayout
{
    Default = 0,
    //NoInput = 1,
    //skinned = 2,
    Imgui = 3,
};

public enum CullMode
{
    None = 1,
    Front = 2,
    Back = 3,
}

public struct PSODesc : IEquatable<PSODesc>
{
    public InputLayout inputLayout;
    public BlendState blendState;
    public CullMode cullMode;
    public Format rtvFormat;
    public Format dsvFormat;
    public int renderTargetCount;
    public int depthBias;
    public float slopeScaledDepthBias;
    public bool wireFrame;

    public override bool Equals(object obj)
    {
        return obj is PSODesc desc && Equals(desc);
    }

    public bool Equals(PSODesc other)
    {
        return inputLayout == other.inputLayout &&
               blendState == other.blendState &&
               cullMode == other.cullMode &&
               rtvFormat == other.rtvFormat &&
               dsvFormat == other.dsvFormat &&
               renderTargetCount == other.renderTargetCount &&
               depthBias == other.depthBias &&
               slopeScaledDepthBias == other.slopeScaledDepthBias &&
               wireFrame == other.wireFrame;
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add((int)inputLayout);
        hash.Add((int)blendState);
        hash.Add((int)cullMode);
        hash.Add((int)rtvFormat);
        hash.Add((int)dsvFormat);
        hash.Add(renderTargetCount);
        hash.Add(depthBias);
        hash.Add(slopeScaledDepthBias);
        hash.Add(wireFrame);
        return hash.ToHashCode();
    }

    public static bool operator ==(PSODesc left, PSODesc right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PSODesc left, PSODesc right)
    {
        return !(left == right);
    }
}
public class PSO : IDisposable
{
    //static readonly InputLayoutDescription inputLayoutDefault = new InputLayoutDescription(
    //    new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
    //    new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 0, 1),
    //    new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0, 2),
    //    new InputElementDescription("TANGENT", 0, Format.R32G32B32A32_Float, 0, 3),
    //    new InputElementDescription("BONES", 0, Format.R16G16B16A16_UInt, 0, 4),
    //    new InputElementDescription("WEIGHTS", 0, Format.R32G32B32A32_Float, 0, 5)
    //    );

    static readonly InputLayoutDescription inputLayoutImGui = new InputLayoutDescription(
        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
        new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
        );
    static readonly BlendDescription blendStateAdd = new BlendDescription(Blend.SourceAlpha, Blend.One);
    static readonly BlendDescription blendStateAlpha = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);
    static readonly BlendDescription blendStatePreserveAlpha = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.Zero, Blend.One);

    BlendDescription BlendDescSelect(BlendState blendState)
    {
        if (blendState == BlendState.None)
            return new BlendDescription(Blend.One, Blend.Zero);
        else if (blendState == BlendState.Alpha)
            return blendStateAlpha;
        else if (blendState == BlendState.Add)
            return blendStateAdd;
        else if (blendState == BlendState.PreserveAlpha)
            return blendStatePreserveAlpha;
        return new BlendDescription();
    }

    public byte[] vertexShader;
    public byte[] pixelShader;
    public byte[] geometryShader;
    public string Name;
    public GraphicsObjectStatus Status;

    public ID3D12ShaderReflection vsReflection;
    public ID3D12ShaderReflection gsReflection;
    public ID3D12ShaderReflection psReflection;

    internal InputLayoutDescription inputLayoutDescription;

    RootSignature rootSignature1;

    internal List<(PSODesc, ID3D12PipelineState)> m_pipelineStates = new List<(PSODesc, ID3D12PipelineState)>();

    public PSO()
    {

    }

    public PSO(byte[] vertexShader, byte[] geometryShader, byte[] pixelShader, ID3D12ShaderReflection vsr, ID3D12ShaderReflection gsr, ID3D12ShaderReflection psr)
    {
        this.vertexShader = vertexShader;
        this.geometryShader = geometryShader;
        this.pixelShader = pixelShader;
        this.vsReflection = vsr;
        this.gsReflection = gsr;
        this.psReflection = psr;

        inputLayoutDescription = GetInputElementDescriptions(vsReflection);
    }

    internal bool TryGetPipelineState(ID3D12Device device, PSODesc psoDesc, out ID3D12PipelineState pipelineState, out RootSignature rs)
    {
        CreateRootSignature();
        rs = this.rootSignature1;



        for (int i = 0; i < m_pipelineStates.Count; i++)
        {
            if (m_pipelineStates[i].Item1 == psoDesc)
            {
                pipelineState = m_pipelineStates[i].Item2;
                return true;
            }
        }
        GraphicsPipelineStateDescription desc = new GraphicsPipelineStateDescription();

        if (psoDesc.inputLayout == InputLayout.Default)
        {
            desc.InputLayout = inputLayoutDescription;
            //desc.InputLayout = inputLayoutDefault;
        }
        else if (psoDesc.inputLayout == InputLayout.Imgui)
            desc.InputLayout = inputLayoutImGui;

        desc.RootSignature = this.rootSignature1.GetRootSignature(device);
        if (vertexShader != null)
            desc.VertexShader = vertexShader;
        if (geometryShader != null)
            desc.GeometryShader = geometryShader;
        if (pixelShader != null)
            desc.PixelShader = pixelShader;
        desc.SampleMask = uint.MaxValue;
        desc.PrimitiveTopologyType = PrimitiveTopologyType.Triangle;
        desc.BlendState = BlendDescSelect(psoDesc.blendState);
        desc.SampleDescription = new SampleDescription(1, 0);

        desc.RenderTargetFormats = new Format[psoDesc.renderTargetCount];
        for (int i = 0; i < psoDesc.renderTargetCount; i++)
        {
            desc.RenderTargetFormats[i] = psoDesc.rtvFormat;
        }
        CullMode cullMode = psoDesc.cullMode;
        if (cullMode == 0) cullMode = CullMode.None;
        RasterizerDescription rasterizerDescription = new RasterizerDescription((Vortice.Direct3D12.CullMode)cullMode, psoDesc.wireFrame ? FillMode.Wireframe : FillMode.Solid);
        rasterizerDescription.DepthBias = psoDesc.depthBias;
        rasterizerDescription.SlopeScaledDepthBias = psoDesc.slopeScaledDepthBias;
        if (psoDesc.dsvFormat != Format.Unknown)
        {
            desc.DepthStencilState = new DepthStencilDescription(true, DepthWriteMask.All, ComparisonFunction.Less);
            desc.DepthStencilFormat = psoDesc.dsvFormat;
            rasterizerDescription.DepthClipEnable = true;
        }
        else
        {
            desc.DepthStencilState = new DepthStencilDescription();
        }

        desc.RasterizerState = rasterizerDescription;
        ID3D12PipelineState pipelineState1;
        if (device.CreateGraphicsPipelineState(desc, out pipelineState1).Failure)
        {
            Status = GraphicsObjectStatus.error;
            pipelineState = null;
            return false;
        }
        m_pipelineStates.Add((psoDesc, pipelineState1));
        pipelineState = pipelineState1;
        return true;
    }


    static InputElementDescription[] GetInputElementDescriptions(ID3D12ShaderReflection reflection)
    {
        int count1 = 0;
        foreach (var item in reflection.InputParameters)
            if (item.SystemValueType == SystemValueType.Undefined)
                count1++;
        var descs = new InputElementDescription[count1];

        int count = 0;
        foreach (var item in reflection.InputParameters)
        {
            if (item.SystemValueType == SystemValueType.Undefined)
            {
                Format format = Format.Unknown;
                if (item.ComponentType == RegisterComponentType.Float32)
                {
                    if (item.MinPrecision == MinPrecision.MinPrecisionFloat16)
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R16G16B16A16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R16G16B16A16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R16G16_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R16_Float;
                    }
                    else
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R32G32B32A32_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R32G32B32_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R32G32_Float;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R32_Float;
                    }
                }
                else if (item.ComponentType == RegisterComponentType.UInt32)
                {
                    if (item.MinPrecision == MinPrecision.MinPrecisionUInt16)
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R16G16B16A16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R16G16B16A16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R16G16_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R16_UInt;
                    }
                    else
                    {
                        if ((item.UsageMask & RegisterComponentMaskFlags.ComponentW) != 0)
                            format = Format.R32G32B32A32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentZ) != 0)
                            format = Format.R32G32B32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentY) != 0)
                            format = Format.R32G32_UInt;
                        else if ((item.UsageMask & RegisterComponentMaskFlags.ComponentX) != 0)
                            format = Format.R32_UInt;
                    }
                }
                descs[count] = new InputElementDescription(item.SemanticName, item.SemanticIndex, format, count);
                count++;
            }
        }
        return descs;
    }


    void CreateRootSignature()
    {
        if (rootSignature1 != null)
            return;

        StaticSamplerDescription[] samplerDescription = new StaticSamplerDescription[4];
        samplerDescription[0] = new StaticSamplerDescription()
        {
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            BorderColor = StaticBorderColor.OpaqueBlack,
            ComparisonFunction = ComparisonFunction.Never,
            Filter = Filter.MinMagMipLinear,
            MipLODBias = 0,
            MaxAnisotropy = 0,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
            ShaderVisibility = ShaderVisibility.All,
            RegisterSpace = 0,
            ShaderRegister = 0,
        };
        samplerDescription[1] = samplerDescription[0] with
        {
            ShaderRegister = 1,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MaxAnisotropy = 16,
            Filter = Filter.Anisotropic
        };

        samplerDescription[2] = samplerDescription[0] with
        {
            ShaderRegister = 2,
            ComparisonFunction = ComparisonFunction.Less,
            Filter = Filter.ComparisonMinMagMipLinear,
        };
        samplerDescription[3] = samplerDescription[0] with
        {
            ShaderRegister = 3,
            Filter = Filter.MinMagMipPoint
        };


        var parameters = new List<RootParameter1>();
        //var samplers = new List<StaticSamplerDescription>();
        var bindings = new List<InputBindingDescription>();
        if (vsReflection != null)
            bindings.AddRange(vsReflection.BoundResources);
        if (gsReflection != null)
            bindings.AddRange(gsReflection.BoundResources);
        if (psReflection != null)
            bindings.AddRange(psReflection.BoundResources);
        HashSet<int> srvVisited = new HashSet<int>();
        HashSet<int> cbvVisited = new HashSet<int>();
        HashSet<int> uavVisited = new HashSet<int>();

        foreach (var res in bindings)
        {
            switch (res.Type)
            {
                case ShaderInputType.Texture:
                case ShaderInputType.Structured:
                    if (!srvVisited.Add(res.BindPoint))
                        continue;
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                                    DescriptorRangeType.ShaderResourceView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                case ShaderInputType.ConstantBuffer:
                    if (!cbvVisited.Add(res.BindPoint))
                        continue;
                    parameters.Add(new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(res.BindPoint, res.Space), ShaderVisibility.All));
                    break;
                case ShaderInputType.Sampler:
                    //samplers.Add(new StaticSamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap,
                    //        0, 16, ComparisonFunction.Never, StaticBorderColor.TransparentBlack, float.MinValue, float.MaxValue, res.BindPoint, 0));
                    break;
                case ShaderInputType.UnorderedAccessViewRWTyped:
                case ShaderInputType.UnorderedAccessViewRWStructured:
                    if (!uavVisited.Add(res.BindPoint))
                        continue;
                    parameters.Add(new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(
                            DescriptorRangeType.UnorderedAccessView, 1, res.BindPoint, res.Space)), ShaderVisibility.All));
                    break;
                default:
                    break;
            }
        }
        //var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.None, parameters.ToArray(), samplers.ToArray());
        var rootSignatureDescription1 = new RootSignatureDescription1(RootSignatureFlags.AllowInputAssemblerInputLayout, parameters.ToArray(), samplerDescription);
        rootSignature1 = new RootSignature();
        rootSignature1.FromDesc(rootSignatureDescription1);
    }

    public void Dispose()
    {
        rootSignature1.Dispose();
        rootSignature1 = null;

        vsReflection?.Release();
        vsReflection = null;
        gsReflection?.Release();
        gsReflection = null;
        psReflection?.Release();
        psReflection = null;
        foreach (var combine in m_pipelineStates)
        {
            combine.Item2.Release();
        }
        m_pipelineStates.Clear();
    }
}
