using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Coocoo3D.RenderPipeline;

public class RenderWrap
{
    public RenderPipelineView RenderPipelineView { get; set; }

    public RenderPipelineContext rpc { get; set; }

    public GraphicsContext graphicsContext { get => rpc.graphicsContext; }

    public string BasePath { get => RenderPipelineView.path; }

    public (int, int) outputSize;

    public void GetOutputSize(out int width, out int height)
    {
        (width, height) = outputSize;
    }

    public void SetSize(string key, int width, int height)
    {
        var texs = RenderPipelineView.RenderTextures.Values.Where(u =>
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
        if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.memberInfo.GetGetterType() == typeof(Texture2D))
        {
            return usage.GetTexture2D();
        }
        return null;
    }

    public Texture2D GetTex2DLoaded(string name)
    {
        return rpc.mainCaches.GetTextureLoaded(Path.GetFullPath(name, BasePath), graphicsContext);
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

    public void SetRenderTarget(Texture2D[] target, Texture2D depth, bool clearRT, bool clearDepth)
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

    public void Swap(string renderTexture1, string renderTexture2)
    {
        var rts = RenderPipelineView.RenderTextures;
        rts.TryGetValue(renderTexture1, out var rt1);
        rts.TryGetValue(renderTexture2, out var rt2);
        if (rt1.width == rt2.width && rt1.height == rt2.height && rt1.resourceFormat == rt2.resourceFormat
            && rt1.depth == rt2.depth && rt1.mips == rt2.mips && rt1.arraySize == rt2.arraySize)
        {
            Texture2D tex1 = rt1.GetTexture2D();
            Texture2D tex2 = rt2.GetTexture2D();
            if (tex1 != null && tex2 != null)
            {
                rt1.memberInfo.SetValue(RenderPipelineView.renderPipeline, tex2);
                rt2.memberInfo.SetValue(RenderPipelineView.renderPipeline, tex1);
            }
            else if (rt1.gpuBuffer != null && rt2.gpuBuffer != null)
            {
                (rt1.gpuBuffer, rt2.gpuBuffer) = (rt2.gpuBuffer, rt1.gpuBuffer);
                rt1.memberInfo.SetValue(RenderPipelineView.renderPipeline, rt1.gpuBuffer);
                rt2.memberInfo.SetValue(RenderPipelineView.renderPipeline, rt2.gpuBuffer);
            }
        }
    }
}
