using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    struct _PSODesc1 : IEquatable<_PSODesc1>
    {
        public PSODesc desc;
        public ID3D12RootSignature rootSignature;

        public override bool Equals(object obj)
        {
            return obj is _PSODesc1 desc && Equals(desc);
        }

        public bool Equals(_PSODesc1 other)
        {
            return desc.Equals(other.desc) &&
                   EqualityComparer<ID3D12RootSignature>.Default.Equals(rootSignature, other.rootSignature);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(desc, rootSignature);
        }

        public static bool operator ==(_PSODesc1 left, _PSODesc1 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(_PSODesc1 left, _PSODesc1 right)
        {
            return !(left == right);
        }
    }
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
        NoInput = 1,
        //skinned = 2,
        Imgui = 3,
        Particle = 4,
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
        static readonly InputLayoutDescription inputLayoutDefault = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 0, 1),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0, 2),
            new InputElementDescription("TANGENT", 0, Format.R32G32B32A32_Float, 0, 3),
            new InputElementDescription("BONES", 0, Format.R16G16B16A16_UInt, 0, 4),
            new InputElementDescription("WEIGHTS", 0, Format.R32G32B32A32_Float, 0, 5)
            );

        //static readonly InputLayoutDescription inputLayoutPosOnly = new InputLayoutDescription(
        //    new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0)
        //    );
        static readonly InputLayoutDescription inputLayoutNoInput = new InputLayoutDescription();
        static readonly InputLayoutDescription inputLayoutImGui = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
            );
        static readonly InputLayoutDescription inputLayoutParticle = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 0),
            new InputElementDescription("SIZE", 0, Format.R32_Float, 0)
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

        internal List<(_PSODesc1, ID3D12PipelineState)> m_pipelineStates = new List<(_PSODesc1, ID3D12PipelineState)>();

        public PSO()
        {

        }

        public PSO(byte[] vertexShader, byte[] geometryShader, byte[] pixelShader)
        {
            this.vertexShader = vertexShader;
            this.geometryShader = geometryShader;
            this.pixelShader = pixelShader;
        }

        public void Dispose()
        {
            foreach (var combine in m_pipelineStates)
            {
                combine.Item2.Release();
            }
            m_pipelineStates.Clear();
        }

        internal bool TryGetPipelineState(GraphicsDevice graphicsDevice, RootSignature graphicsSignature, PSODesc psoDesc, out ID3D12PipelineState pipelineState)
        {
            _PSODesc1 _psoDesc1;
            _psoDesc1.desc = psoDesc;
            _psoDesc1.rootSignature = graphicsSignature.rootSignature;

            for (int i = 0; i < m_pipelineStates.Count; i++)
            {
                if (m_pipelineStates[i].Item1 == _psoDesc1)
                {
                    pipelineState = m_pipelineStates[i].Item2;
                    return true;
                }
            }
            GraphicsPipelineStateDescription state = new GraphicsPipelineStateDescription();

            if (psoDesc.inputLayout == InputLayout.Default)
                state.InputLayout = inputLayoutDefault;
            else if (psoDesc.inputLayout == InputLayout.NoInput)
                state.InputLayout = inputLayoutNoInput;
            else if (psoDesc.inputLayout == InputLayout.Imgui)
                state.InputLayout = inputLayoutImGui;
            else if (psoDesc.inputLayout == InputLayout.Particle)
                state.InputLayout = inputLayoutParticle;

            state.RootSignature = graphicsSignature.rootSignature;
            if (vertexShader != null)
                state.VertexShader = vertexShader;
            if (geometryShader != null)
                state.GeometryShader = geometryShader;
            if (pixelShader != null)
                state.PixelShader = pixelShader;
            state.SampleMask = uint.MaxValue;
            state.PrimitiveTopologyType = PrimitiveTopologyType.Triangle;
            state.BlendState = BlendDescSelect(psoDesc.blendState);
            state.SampleDescription = new SampleDescription(1, 0);

            state.RenderTargetFormats = new Format[psoDesc.renderTargetCount];
            for (int i = 0; i < psoDesc.renderTargetCount; i++)
            {
                state.RenderTargetFormats[i] = psoDesc.rtvFormat;
            }
            CullMode cullMode = psoDesc.cullMode;
            if (cullMode == 0) cullMode = CullMode.None;
            RasterizerDescription rasterizerDescription = new RasterizerDescription((Vortice.Direct3D12.CullMode)cullMode, psoDesc.wireFrame ? FillMode.Wireframe : FillMode.Solid);
            rasterizerDescription.DepthBias = psoDesc.depthBias;
            rasterizerDescription.SlopeScaledDepthBias = psoDesc.slopeScaledDepthBias;
            if (psoDesc.dsvFormat != Format.Unknown)
            {
                state.DepthStencilState = new DepthStencilDescription(true, DepthWriteMask.All, ComparisonFunction.Less);
                state.DepthStencilFormat = psoDesc.dsvFormat;
                rasterizerDescription.DepthClipEnable = true;
            }
            else
            {
                state.DepthStencilState = new DepthStencilDescription();
            }

            state.RasterizerState = rasterizerDescription;
            ID3D12PipelineState pipelineState1;
            if (graphicsDevice.device.CreateGraphicsPipelineState(state, out pipelineState1).Failure)
            {
                Status = GraphicsObjectStatus.error;
                pipelineState = null;
                return false;
            }
            m_pipelineStates.Add((_psoDesc1, pipelineState1));
            pipelineState = pipelineState1;
            return true;
        }
    }
}
