using Coocoo3D.Present;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Coocoo3D.RenderPipeline
{
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
            if (textures == null)
                return;
            var graphicsContext = this.graphicsContext;
            for (int i = 0; i < textures.Count; i++)
            {
                string texture = textures[i];
                if (RenderPipelineView.RenderTextures.TryGetValue(texture, out var usage))
                {
                    if (usage.texture2D != null)
                    {
                        if (usage.srgbAttribute != null)
                            graphicsContext.SetSRVTSlot(GetTex2DFallBack(texture, material), i);
                        else
                            graphicsContext.SetSRVTSlotLinear(GetTex2DFallBack(texture, material), i);
                    }
                    else if (usage.gpuBuffer != null)
                    {
                        graphicsContext.SetSRVTSlot(usage.gpuBuffer, i);
                    }
                }
            }
        }

        public void SetUAVs(IReadOnlyList<string> textures, RenderMaterial material = null)
        {
            if (textures == null)
                return;
            var graphicsContext = this.graphicsContext;
            for (int i = 0; i < textures.Count; i++)
            {
                string texture = textures[i];
                if (RenderPipelineView.RenderTextures.TryGetValue(texture, out var usage))
                {
                    if (usage.texture2D != null)
                        graphicsContext.SetUAVTSlot(GetRenderTexture2D(texture), i);
                    else if (usage.gpuBuffer != null)
                        graphicsContext.SetUAVTSlot(usage.gpuBuffer, i);
                }
            }
        }

        public void SetUAV(Texture2D texture2D, int slot)
        {
            graphicsContext.SetUAVTSlot(texture2D, slot);
        }

        public void SetUAV(GPUBuffer buffer, int slot)
        {
            graphicsContext.SetUAVTSlot(buffer, slot);
        }

        public void SetUAV(Texture2D texture2D, int mipIndex, int slot)
        {
            graphicsContext.SetUAVTSlot(texture2D, mipIndex, slot);
        }

        public void SetSRV(Texture2D texture, int slot)
        {
            graphicsContext.SetSRVTSlot(texture, slot);
        }

        public void SetSRV(GPUBuffer buffer, int slot)
        {
            graphicsContext.SetSRVTSlot(buffer, slot);
        }

        public void SetSRVLim(Texture2D texture, int mip, int slot)
        {
            graphicsContext.SetSRVTLim(texture, mip, slot);
        }

        public Texture2D GetTex2D(string name, RenderMaterial material = null)
        {
            return GetTex2D(name, out _, material);
        }

        public Texture2D GetTex2D(string name, out bool isLinear, RenderMaterial material = null)
        {
            isLinear = false;
            if (string.IsNullOrEmpty(name))
                return null;

            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.texture2D != null)
            {
                isLinear = usage.srgbAttribute == null;
                if (material != null && material.Parameters.TryGetValue(name, out var o1) && o1 is string texturePath)
                    return GetTex2DByName(texturePath);

                if (RenderPipelineView.textureReplacement.TryGetValue(name, out var replacement))
                    return GetTex2DByName(replacement);

                return usage.texture2D;
            }

            return null;
        }

        public bool SlotIsLinear(string name)
        {
            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage))
            {
                return usage.srgbAttribute == null;
            }
            return false;
        }

        public Texture2D GetRenderTexture2D(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.texture2D != null)
            {
                return usage.texture2D;
            }
            return null;
        }

        public Texture2D GetTex2DLoaded(string name)
        {
            return rpc.mainCaches.GetTextureLoaded(Path.GetFullPath(name, BasePath), graphicsContext);
        }

        internal Texture2D GetTex2DByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            else if (rpc.mainCaches.TryGetTexture(name, out var tex))
                return tex;
            return null;
        }

        public string GetTex2DPath(string name, RenderMaterial material = null)
        {
            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.texture2D != null)
            {
                if (material != null && material.Parameters.TryGetValue(name, out var o1) && o1 is string texturePath)
                    return texturePath;

                if (RenderPipelineView.textureReplacement.TryGetValue(name, out var replacement))
                    return replacement;
            }
            return null;
        }

        RootSignature rootSignature;

        public void SetShader(string path, in PSODesc desc, IReadOnlyList<(string, string)> keywords = null, bool vs = true, bool ps = true, bool gs = false)
        {
            var cache = rpc.mainCaches;

            var shaderPath = Path.GetFullPath(path, this.BasePath);
            var pso = cache.GetPSOWithKeywords(keywords, shaderPath, vs, ps, gs);
            if (pso != null)
                graphicsContext.SetPSO(pso, desc);
            else
                Console.WriteLine("shader compilation error");
        }


        public Texture2D texLoading;
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
            return TextureStatusSelect(GetTex2D(name, material), texLoading, texError, texError);
        }
        public static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
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

        public void SetRenderTarget(Texture2D texture2D, bool clear)
        {
            graphicsContext.SetRTV(texture2D, Vector4.Zero, clear);
        }

        public void SetRenderTarget(Texture2D target, Texture2D depth, bool clearRT, bool clearDepth)
        {
            graphicsContext.SetRTVDSV(target, depth, Vector4.Zero, clearRT, clearDepth);
        }
        public void SetRenderTarget(IReadOnlyList<string> rts, string depthStencil1, bool clearRT, bool clearDepth)
        {
            Texture2D depthStencil = GetTex2D(depthStencil1);
            Texture2D[] renderTargets = null;

            if (rts == null || rts.Count == 0)
            {
                if (depthStencil != null)
                    graphicsContext.SetDSV(depthStencil, clearDepth);
            }
            else
            {
                renderTargets = new Texture2D[rts.Count];
                for (int i = 0; i < rts.Count; i++)
                {
                    renderTargets[i] = GetTex2D(rts[i]);
                }
                graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, clearRT, clearDepth);
            }
        }

        public void ClearTexture(Texture2D texture)
        {
            graphicsContext.ClearTexture(texture, Vector4.Zero, 1.0f);
        }

        public void ClearTexture(string texture)
        {
            graphicsContext.ClearTexture(GetTex2D(texture), Vector4.Zero, 1.0f);
        }

        public void SetScissorRectAndViewport(int left, int top, int right, int bottom)
        {
            graphicsContext.RSSetScissorRectAndViewport(left, top, right, bottom);
        }

        public void SetCBV(CBuffer cBuffer, int slot)
        {
            graphicsContext.SetCBVRSlot(cBuffer, slot);
        }

        public void SetRootSignature(string rs)
        {
            rootSignature = rpc.mainCaches.GetRootSignature(rs);
            graphicsContext.SetRootSignature(rootSignature);
        }

        public void Dispatch(string computeShader, IReadOnlyList<(string, string)> keywords, int x = 1, int y = 1, int z = 1)
        {

            var shader = rpc.mainCaches.GetComputeShaderWithKeywords(keywords, Path.GetFullPath(computeShader, BasePath));
            if (shader != null)
            {
                graphicsContext.SetPSO(shader);
                graphicsContext.Dispatch(x, y, z);
            }
            else
            {
                Console.Write("Dispatch faild. " + computeShader);
            }
        }

        public void AfterRender()
        {
            rootSignature = null;
        }

        public GPUWriter Writer { get => rpc.gpuWriter; }

        public void Swap(string renderTexture1, string renderTexture2)
        {
            var rts = RenderPipelineView.RenderTextures;
            rts.TryGetValue(renderTexture1, out var rt1);
            rts.TryGetValue(renderTexture2, out var rt2);
            if (rt1.width == rt2.width && rt1.height == rt2.height && rt1.resourceFormat == rt2.resourceFormat
                && rt1.depth == rt2.depth && rt1.mips == rt2.mips && rt1.arraySize == rt2.arraySize)
            {
                if (rt1.texture2D != null && rt2.texture2D != null)
                {
                    (rt1.texture2D, rt2.texture2D) = (rt2.texture2D, rt1.texture2D);
                    rt1.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt1.texture2D);
                    rt2.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt2.texture2D);
                }
                else if (rt1.gpuBuffer != null && rt2.gpuBuffer != null)
                {
                    (rt1.gpuBuffer, rt2.gpuBuffer) = (rt2.gpuBuffer, rt1.gpuBuffer);
                    rt1.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt1.gpuBuffer);
                    rt2.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt2.gpuBuffer);
                }
            }
        }

        public void CopyTexture(Texture2D target, Texture2D source)
        {
            graphicsContext.CopyTexture(target, source);
        }

    }
}
