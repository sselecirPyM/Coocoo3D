using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using ImGuiNET;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace Coocoo3D.Core;

public sealed class UIRenderSystem : IDisposable
{
    string workDir = System.Environment.CurrentDirectory;
    string imguiShaderPath;

    string errorTexturePath;

    int ptrCount = 100000000;
    Dictionary<IntPtr, Texture2D> viewTextures = new Dictionary<IntPtr, Texture2D>();

    public GraphicsContext graphicsContext;
    public SwapChain swapChain;
    public MainCaches caches;

    public const int uiTextureIndex = 200000000;
    public Texture2D uiTexture;

    public UIRenderSystem()
    {
        imguiShaderPath = System.IO.Path.GetFullPath("Shaders/ImGui.hlsl", workDir);
        errorTexturePath = System.IO.Path.GetFullPath("Assets/Textures/error.png", workDir);
    }

    public IntPtr ShowTexture(Texture2D texture)
    {
        ptrCount++;
        IntPtr ptr = new IntPtr(ptrCount);
        viewTextures[ptr] = texture;
        return ptr;
    }

    public void Update()
    {
        viewTextures[new IntPtr(uiTextureIndex)] = uiTexture;
        Texture2D texError = caches.GetTextureLoaded(errorTexturePath, graphicsContext);

        graphicsContext.SetRenderTargetSwapChain(swapChain, new Vector4(0, 0.3f, 0.3f, 0), true);

        var data = ImGui.GetDrawData();
        if (data.CmdListsCount == 0) return;
        float L = data.DisplayPos.X;
        float R = data.DisplayPos.X + data.DisplaySize.X;
        float T = data.DisplayPos.Y;
        float B = data.DisplayPos.Y + data.DisplaySize.Y;

        Vector2 displayPosition = data.DisplayPos;


        PSODesc desc = new PSODesc
        {
            blendState = BlendState.Alpha,
            cullMode = CullMode.None,
            depthBias = 0,
            slopeScaledDepthBias = 0,
            dsvFormat = Format.Unknown,
            rtvFormat = swapChain.format,
            renderTargetCount = 1,
            wireFrame = false,
            inputLayout = InputLayout.Imgui
        };
        var pso = caches.GetPSO(null, imguiShaderPath);
        graphicsContext.SetPSO(pso, desc);

        ReadOnlySpan<float> imguiCbv = stackalloc float[4] { 2.0f / (R - L), 2.0f / (T - B), (R + L) / (L - R), (T + B) / (B - T) };
        graphicsContext.SetCBVRSlot(0, MemoryMarshal.AsBytes(imguiCbv));

        unsafe
        {
            int vertexSize = data.TotalVtxCount * sizeof(ImDrawVert);
            int indexSize = data.TotalIdxCount * sizeof(UInt16);
            var pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(vertexSize + indexSize);
            Span<byte> vertexDatas = new Span<byte>(buffer, 0, vertexSize);
            Span<byte> indexDatas = new Span<byte>(buffer, vertexSize, indexSize);

            var vertexWriter = new SpanWriter<byte>(vertexDatas);
            var indexWriter = new SpanWriter<byte>(indexDatas);
            for (int i = 0; i < data.CmdListsCount; i++)
            {
                var cmdList = data.CmdListsRange[i];
                var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                var indexBytes = cmdList.IdxBuffer.Size * sizeof(UInt16);
                vertexWriter.Write(new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes));
                indexWriter.Write(new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes));
            }
            graphicsContext.SetMesh(vertexDatas, indexDatas, data.TotalVtxCount, data.TotalIdxCount);
            pool.Return(buffer);
        }
        int vtxOfs = 0;
        int idxOfs = 0;
        for (int i = 0; i < data.CmdListsCount; i++)
        {
            var cmdList = data.CmdListsRange[i];
            var cmdBuffer = cmdList.CmdBuffer;
            for (int j = 0; j < cmdBuffer.Size; j++)
            {
                var cmd = cmdBuffer[j];

                if (!viewTextures.TryGetValue(cmd.TextureId, out var tex))
                {

                }

                tex = TextureStatusSelect(tex, texError);

                graphicsContext.SetSRVTSlotLinear(0, tex);//srgb to srgb
                var rect = cmd.ClipRect;
                graphicsContext.RSSetScissorRect((int)(rect.X - displayPosition.X), (int)(rect.Y - displayPosition.Y), (int)(rect.Z - displayPosition.X), (int)(rect.W - displayPosition.Y));
                graphicsContext.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset) + idxOfs, (int)(cmd.VtxOffset) + vtxOfs);
            }
            vtxOfs += cmdList.VtxBuffer.Size;
            idxOfs += cmdList.IdxBuffer.Size;
        }
        viewTextures.Clear();
        ptrCount = 100000000;
    }

    static Texture2D TextureStatusSelect(Texture2D texture, Texture2D error)
    {
        if (texture?.resource == null)
            return error;
        return texture;
    }

    public void Dispose()
    {
        uiTexture?.Dispose();
    }
}
