using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Coocoo3D.RenderPipeline;

partial class RenderPipelineView
{

    public void GetOutputSize(out int width, out int height)
    {
        (width, height) = outputSize;
    }

    public void SetSize(string key, int width, int height)
    {
        var texs = RenderTextures.Values.Where(u =>
        {
            return u.sizeAttribute != null && u.sizeAttribute.Source == key;
        });
        foreach (var tex in texs)
        {
            tex.width = width;
            tex.height = height;
        }
    }

    public void Draw(int indexCount, int startIndexLocation, int baseVertexLocation)
    {
        graphicsContext.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
    }

    public void Draw(int indexCount, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
    {
        graphicsContext.DrawIndexedInstanced(indexCount, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
    }

    public void SetSRVs(params Texture2D[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            graphicsContext.SetSRVTSlot(i, textures[i]);
        }
    }

    public void SetUAV(int slot, Texture2D texture2D)
    {
        graphicsContext.SetUAVTSlot(slot, texture2D);
    }

    public void SetUAV(int slot, GPUBuffer buffer)
    {
        graphicsContext.SetUAVTSlot(slot, buffer);
    }

    public void SetUAV(int slot, Texture2D texture2D, int mipIndex)
    {
        graphicsContext.SetUAVTSlot(slot, texture2D, mipIndex);
    }

    public void SetSRV(int slot, Texture2D texture)
    {
        graphicsContext.SetSRVTSlot(slot, texture);
    }

    public void SetSRV(int slot, GPUBuffer buffer)
    {
        graphicsContext.SetSRVTSlot(slot, buffer);
    }

    public void SetSRVMip(int slot, Texture2D texture, int mip)
    {
        graphicsContext.SetSRVTMip(slot, texture, mip);
    }

    public Texture2D GetRenderTexture2D(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (RenderTextures.TryGetValue(name, out var usage) && usage.memberInfo.GetGetterType() == typeof(Texture2D))
        {
            return usage.GetTexture2D();
        }
        return null;
    }

    public void SetShader(string path, PSODesc desc, IReadOnlyList<(string, string)> keywords = null)
    {
        var cache = rpc.mainCaches;

        var shaderPath = Path.GetFullPath(path, this.BasePath);
        var pso = cache.GetPSO(keywords, shaderPath);

        if (pso != null)
            graphicsContext.SetPSO(pso, desc);
        else
            Console.WriteLine("shader compilation error");
    }

    public void SetPSO(PSO pso, PSODesc desc)
    {
        graphicsContext.SetPSO(pso, desc);
    }

    public void SetPSO(ComputeShader computeShader)
    {
        graphicsContext.SetPSO(computeShader);
    }
    public Texture2D texError;

    public IReadOnlyList<Texture2D> RenderTargets => _renderTargets;
    List<Texture2D> _renderTargets = new List<Texture2D>();
    public Texture2D depthStencil = null;

    public void SetRenderTarget(Texture2D texture2D, bool clear)
    {
        _renderTargets.Clear();
        _renderTargets.Add(texture2D);
        depthStencil = null;
        graphicsContext.SetRTV(texture2D, Vector4.Zero, clear);
    }

    public void SetRenderTarget(Texture2D target, Texture2D depth, bool clearRT, bool clearDepth)
    {
        _renderTargets.Clear();
        _renderTargets.Add(target);
        depthStencil = depth;
        graphicsContext.SetRTVDSV(target, depth, Vector4.Zero, clearRT, clearDepth);
    }

    public void SetRenderTarget(ReadOnlySpan<Texture2D> target, Texture2D depth, bool clearRT, bool clearDepth)
    {
        _renderTargets.Clear();
        _renderTargets.AddRange(target);
        depthStencil = depth;
        graphicsContext.SetRTVDSV(target, depth, Vector4.Zero, clearRT, clearDepth);
    }

    public void SetRenderTargetDepth(Texture2D depth, bool clearDepth)
    {
        _renderTargets.Clear();
        depthStencil = depth;
        graphicsContext.SetDSV(depth, clearDepth);
    }

    public void Dispatch(int x = 1, int y = 1, int z = 1)
    {
        graphicsContext.Dispatch(x, y, z);
    }
}
