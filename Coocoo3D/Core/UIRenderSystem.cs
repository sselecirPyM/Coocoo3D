﻿using Coocoo3D.RenderPipeline;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using Coocoo3DGraphics.Management;
using ImGuiNET;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using Vortice.DXGI;

namespace Coocoo3D.Core;

public sealed class UIRenderSystem : IDisposable
{
    GPUWriter GPUWriter = new GPUWriter();
    string workDir = System.Environment.CurrentDirectory;
    string imguiShaderPath;

    string loadingTexturePath;
    string errorTexturePath;

    int ptrCount = 100000000;
    Dictionary<IntPtr, Texture2D> viewTextures = new Dictionary<IntPtr, Texture2D>();

    public GraphicsContext graphicsContext;
    public SwapChain swapChain;
    public MainCaches caches;

    public DX12ResourceManager DX12ResourceManager;

    public const int uiTextureIndex = 200000000;
    public Texture2D uiTexture;

    public UIRenderSystem()
    {
        imguiShaderPath = System.IO.Path.GetFullPath("Shaders/ImGui.hlsl", workDir);
        loadingTexturePath = System.IO.Path.GetFullPath("Assets/Textures/loading.png", workDir);
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
        Texture2D texLoading = caches.GetTextureLoaded(loadingTexturePath, graphicsContext);
        Texture2D texError = caches.GetTextureLoaded(errorTexturePath, graphicsContext);

        var rs = DX12ResourceManager.GetRootSignature("Cs");

        graphicsContext.SetRootSignature(rs);

        graphicsContext.SetRenderTargetSwapChain(swapChain, new Vector4(0, 0.3f, 0.3f, 0), true);

        var data = ImGui.GetDrawData();
        if (data.CmdListsCount == 0) return;
        float L = data.DisplayPos.X;
        float R = data.DisplayPos.X + data.DisplaySize.X;
        float T = data.DisplayPos.Y;
        float B = data.DisplayPos.Y + data.DisplaySize.Y;

        Vector2 displayPosition = data.DisplayPos;


        PSODesc desc;
        desc.blendState = BlendState.Alpha;
        desc.cullMode = CullMode.None;
        desc.depthBias = 0;
        desc.slopeScaledDepthBias = 0;
        desc.dsvFormat = Format.Unknown;
        desc.rtvFormat = swapChain.format;
        desc.renderTargetCount = 1;
        desc.wireFrame = false;
        desc.inputLayout = InputLayout.Imgui;
        var pso = caches.GetPSO(null, imguiShaderPath);
        graphicsContext.SetPSO(pso.pso, desc);

        Vector4 scaleTrans = new Vector4(2.0f / (R - L), 2.0f / (T - B), (R + L) / (L - R), (T + B) / (B - T));
        GPUWriter.graphicsContext = graphicsContext;
        GPUWriter.Write(scaleTrans);
        GPUWriter.SetCBV(0);
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

                tex = TextureStatusSelect(tex, texLoading, texError, texError);

                graphicsContext.SetSRVTSlotLinear(tex, 0);//srgb to srgb
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

    static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
    {
        if (texture == null)
            return error;
        return texture.Status switch
        {
            GraphicsObjectStatus.loaded => texture,
            GraphicsObjectStatus.loading => loading,
            GraphicsObjectStatus.unload => unload,
            _ => error
        };
    }
    public void Dispose()
    {
        uiTexture?.Dispose();
    }
}
