using Coocoo3D.Present;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

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

    public void SetSRVs(IReadOnlyList<string> textures, RenderMaterial material = null)
    {
        SetSRVs(textures, material?.Parameters);
    }

    public void SetSRVs(IReadOnlyList<string> textures, IDictionary<string, object> material)
    {
        if (textures == null)
            return;
        var graphicsContext = this.graphicsContext;
        for (int i = 0; i < textures.Count; i++)
        {
            string texture = textures[i];
            if (RenderPipelineView.RenderTextures.TryGetValue(texture, out var usage))
            {
                if (usage.memberInfo.GetGetterType() == typeof(Texture2D))
                {
                    graphicsContext.SetSRVTSlot(i, GetTex2DFallBack(texture, material));
                }
                else if (usage.memberInfo.GetGetterType() == typeof(GPUBuffer))
                {
                    graphicsContext.SetSRVTSlot(i, usage.gpuBuffer);
                }
            }
        }
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

    public Texture2D GetTex2D(string name, RenderMaterial material = null)
    {
        return GetTex2D(name, material?.Parameters);
    }

    public Texture2D GetTex2D(string name, IDictionary<string, object> material)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.memberInfo.GetGetterType() == typeof(Texture2D))
        {
            if (material != null && material.TryGetValue(name, out var o1))
            {
                if (o1 is string texturePath)
                    return GetTex2DByName(texturePath);
                else if (o1 is Texture2D texture)
                    return texture;
            }

            return usage.GetTexture2D();
        }

        return null;
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

    private Texture2D GetTex2DByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        else if (rpc.mainCaches.TryGetTexture(name, out var tex))
            return tex;
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

    public object GetResourceFallBack(string name, RenderMaterial material = null)
    {
        if (RenderPipelineView.RenderTextures.TryGetValue(name, out var tex))
        {
            if (tex.gpuBuffer != null)
                return tex.gpuBuffer;
        }
        return GetTex2DFallBack(name, material);
    }

    public Texture2D GetTex2DFallBack(string name, RenderMaterial material = null)
    {
        return TextureStatusSelect(GetTex2D(name, material));
    }

    public Texture2D GetTex2DFallBack(string name, IDictionary<string, object> material)
    {
        return TextureStatusSelect(GetTex2D(name, material));
    }

    public Texture2D GetTex2DByNameFallBack(string name)
    {
        return TextureStatusSelect(GetTex2DByName(name));
    }

    public Texture2D TextureStatusSelect(Texture2D texture)
    {
        if (texture == null || texture.Status == GraphicsObjectStatus.unload)
        {
            texError.Status = GraphicsObjectStatus.error;
            return texError;
        }

        return texture;
    }

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
    public void SetRenderTarget(IReadOnlyList<string> rts, string depthStencil1, bool clearRT, bool clearDepth)
    {
        _renderTargets.Clear();
        depthStencil = GetTex2D(depthStencil1);


        if (rts == null || rts.Count == 0)
        {
            if (depthStencil != null)
                graphicsContext.SetDSV(depthStencil, clearDepth);
        }
        else
        {
            for (int i = 0; i < rts.Count; i++)
            {
                _renderTargets.Add(GetTex2D(rts[i]));
            }
            graphicsContext.SetRTVDSV(CollectionsMarshal.AsSpan(_renderTargets), depthStencil, Vector4.Zero, clearRT, clearDepth);
        }
    }
    public void SetRenderTargetDepth(Texture2D depth, bool clearDepth)
    {
        _renderTargets.Clear();
        depthStencil = depth;
        graphicsContext.SetDSV(depth, clearDepth);
    }

    public void ClearTexture(Texture2D texture)
    {
        graphicsContext.ClearTexture(texture, Vector4.Zero, 1.0f);
    }

    public void SetScissorRectAndViewport(int left, int top, int right, int bottom)
    {
        graphicsContext.RSSetScissorRectAndViewport(left, top, right, bottom);
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

    public void CopyTexture(Texture2D target, Texture2D source)
    {
        graphicsContext.CopyTexture(target, source);
    }

}
