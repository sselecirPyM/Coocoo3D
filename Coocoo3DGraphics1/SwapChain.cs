using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class SwapChain
    {
        internal IDXGISwapChain3 m_swapChain;
        internal ID3D12Resource[] m_renderTargets = new ID3D12Resource[c_frameCount];
        internal ResourceStates[] renderTargetResourceStates = new ResourceStates[c_frameCount];

        public int width { get; private set; }
        public int height { get; private set; }

        IntPtr hwnd;

        public Format format { get; private set; }

        GraphicsDevice device;

        public void Initialize(GraphicsDevice device, IntPtr hwnd, float width, float height, Format format = Format.R8G8B8A8_UNorm)
        {
            this.format = format;
            this.hwnd = hwnd;
            this.width = Math.Max((int)Math.Round(width), 1);
            this.height = Math.Max((int)Math.Round(height), 1);
            this.device = device;
            Resize();
        }

        public void Resize(float width, float height)
        {
            int width1 = Math.Max((int)Math.Round(width), 1);
            int height1 = Math.Max((int)Math.Round(height), 1);

            if (width1 != this.width || height1 != this.height)
            {
                this.width = width1;
                this.height = height1;
                Resize();
            }
        }

        internal void Resize()
        {
            // 等到以前的所有 GPU 工作完成。
            device.WaitForGpu();

            // 清除特定于先前窗口大小的内容。
            for (int n = 0; n < c_frameCount; n++)
            {
                m_renderTargets[n]?.Dispose();
                m_renderTargets[n] = null;
                renderTargetResourceStates[n] = ResourceStates.Common;
            }

            if (m_swapChain != null)
            {
                // 如果交换链已存在，请调整其大小。
                Result hr = m_swapChain.ResizeBuffers(c_frameCount, this.width, this.height, format, SwapChainFlags.AllowTearing);

                ThrowIfFailed(hr);
            }
            else
            {
                // 否则，使用与现有 Direct3D 设备相同的适配器新建一个。
                SwapChainDescription1 swapChainDescription1 = new SwapChainDescription1();

                swapChainDescription1.Width = this.width;                      // 匹配窗口的大小。
                swapChainDescription1.Height = this.height;
                swapChainDescription1.Format = format;
                swapChainDescription1.Stereo = false;
                swapChainDescription1.SampleDescription.Count = 1;                         // 请不要使用多采样。
                swapChainDescription1.SampleDescription.Quality = 0;
                swapChainDescription1.Usage = Usage.RenderTargetOutput;
                swapChainDescription1.BufferCount = c_frameCount;                   // 使用三重缓冲最大程度地减小延迟。
                swapChainDescription1.SwapEffect = SwapEffect.FlipSequential;
                swapChainDescription1.Flags = SwapChainFlags.AllowTearing;
                swapChainDescription1.Scaling = Scaling.Stretch;
                swapChainDescription1.AlphaMode = AlphaMode.Ignore;

                var swapChain = device.m_dxgiFactory.CreateSwapChainForHwnd(device.commandQueue, hwnd, swapChainDescription1);
                m_swapChain?.Dispose();
                m_swapChain = swapChain.QueryInterface<IDXGISwapChain3>();
                swapChain.Dispose();
            }

            for (int n = 0; n < c_frameCount; n++)
            {
                ThrowIfFailed(m_swapChain.GetBuffer(n, out m_renderTargets[n]));
            }
        }

        internal void Present(bool vsync)
        {
            Result hr;
            if (vsync)
            {
                hr = m_swapChain.Present(1, 0);
            }
            else
            {
                hr = m_swapChain.Present(0, PresentFlags.AllowTearing);
            }

            ThrowIfFailed(hr);
        }

        internal void EndRenderTarget(ID3D12GraphicsCommandList graphicsCommandList)
        {
            int index = m_swapChain.GetCurrentBackBufferIndex();
            var state = renderTargetResourceStates[index];
            var stateAfter = ResourceStates.Present;
            if (state != stateAfter)
            {
                graphicsCommandList.ResourceBarrierTransition(m_renderTargets[index], state, stateAfter);
                renderTargetResourceStates[index] = stateAfter;
            }
        }

        internal ID3D12Resource GetResource(ID3D12GraphicsCommandList graphicsCommandList)
        {
            int index = m_swapChain.GetCurrentBackBufferIndex();
            var state = renderTargetResourceStates[index];
            var stateAfter = ResourceStates.RenderTarget;
            if (state != stateAfter)
            {
                graphicsCommandList.ResourceBarrierTransition(m_renderTargets[index], state, stateAfter);
                renderTargetResourceStates[index] = stateAfter;
            }
            return m_renderTargets[index];
        }
    }
}
