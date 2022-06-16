using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caprice;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System.Numerics;
using Coocoo3D.Components;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.Utility;
using System.Reflection;
using Caprice.Attributes;

namespace Coocoo3D.RenderPipeline
{
    public class RenderWrap
    {
        public RenderPipelineView RenderPipelineView { get; set; }

        public RenderPipelineContext rpc { get; set; }

        public GraphicsContext graphicsContext { get => rpc.graphicsContext; }

        public VisualChannel visualChannel;

        public string BasePath { get => RenderPipelineView.path; }

        public bool CPUSkinning { get => rpc.CPUSkinning; set => rpc.CPUSkinning = value; }

        public float Time { get => (float)rpc.dynamicContextRead.Time; }
        public float DeltaTime { get => (float)rpc.dynamicContextRead.DeltaTime; }
        public float RealDeltaTime { get => (float)rpc.dynamicContextRead.RealDeltaTime; }

        public IEnumerable<VisualComponent> Visuals { get => rpc.dynamicContextRead.visuals; }

        public CameraData Camera { get => visualChannel.cameraData; }

        List<(object, Dictionary<string, MemberInfo>)> dataStack = new();

        Dictionary<Type, Dictionary<string, MemberInfo>> memberInfoCache = new();

        public bool Recording { get => rpc.recording; }

        public void GetOutputSize(out int width, out int height)
        {
            (width, height) = visualChannel.outputSize;
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

        public void Draw(in MeshRenderable renderable)
        {
            graphicsContext.DrawIndexed(renderable.indexCount, renderable.indexStart, renderable.vertexStart);
        }

        public void Draw(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            graphicsContext.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
        }

        public void DrawQuad(int instanceCount = 1)
        {
            var graphicsContext = this.graphicsContext;
            graphicsContext.SetMesh(rpc.quadMesh);
            graphicsContext.DrawIndexedInstanced(6, instanceCount, 0, 0, 0);
        }

        public void DrawCube(int instanceCount = 1)
        {
            var graphicsContext = this.graphicsContext;
            graphicsContext.SetMesh(rpc.cubeMesh);
            graphicsContext.DrawIndexedInstanced(36, instanceCount, 0, 0, 0);
        }

        public void SetSRVs(IReadOnlyList<string> textures, RenderMaterial material = null)
        {
            if (textures == null) return;
            var graphicsContext = this.graphicsContext;
            for (int i = 0; i < textures.Count; i++)
            {
                string texture = textures[i];
                if (RenderPipelineView.RenderTextures.TryGetValue(texture, out var usage))
                {
                    if (usage.textureCube != null)
                        graphicsContext.SetSRVTSlot(GetTexCube(usage), i);
                    else if (usage.texture2D != null)
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
            if (textures == null) return;
            var graphicsContext = this.graphicsContext;
            for (int i = 0; i < textures.Count; i++)
            {
                string texture = textures[i];
                if (RenderPipelineView.RenderTextures.TryGetValue(texture, out var usage))
                {
                    if (usage.textureCube != null)
                        graphicsContext.SetUAVTSlot(GetTexCube(usage), i);
                    else if (usage.texture2D != null)
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

        public void SetUAV(TextureCube textureCube, int slot)
        {
            graphicsContext.SetUAVTSlot(textureCube, slot);
        }

        public void SetUAV(GPUBuffer buffer, int slot)
        {
            graphicsContext.SetUAVTSlot(buffer, slot);
        }

        public void SetSRV(Texture2D texture, int slot)
        {
            graphicsContext.SetSRVTSlot(texture, slot);
        }

        public void SetSRV(GPUBuffer buffer, int slot)
        {
            graphicsContext.SetSRVTSlot(buffer, slot);
        }

        public void SetSRV(TextureCube textureCube, int slot)
        {
            graphicsContext.SetSRVTSlot(textureCube, slot);
        }

        public void SetSRVLim(TextureCube textureCube, int mip, int slot)
        {
            graphicsContext.SetSRVTLim(textureCube, mip, slot);
        }

        public void SetUAV(TextureCube textureCube, int mipIndex, int slot)
        {
            graphicsContext.SetUAVTSlot(textureCube, mipIndex, slot);
        }

        TextureCube GetTexCube(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.textureCube != null)
                return usage.textureCube;
            return null;
        }

        TextureCube GetTexCube(RenderTextureUsage usage)
        {
            if (usage == null)
                return null;

            return usage.textureCube;
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

        public TextureCube GetTexCube(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (RenderPipelineView.RenderTextures.TryGetValue(name, out var usage) && usage.textureCube != null)
            {
                //string texturePath = GetTex2DPath(name, material);
                //if (!string.IsNullOrEmpty(texturePath))
                //    return _GetTex2DByName(texturePath);

                //if (RenderPipelineView.textureReplacement.TryGetValue(name, out var replacement))
                //    return _GetTex2DByName(replacement);

                return usage.textureCube;
            }

            return null;
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
        }

        //public TextureCube GetTexCube(string name, RenderMaterial material = null)
        //{
        //    if (string.IsNullOrEmpty(name))
        //        return null;

        //    TextureCube tex2D;
        //    if (passSetting.RenderTargetCubes.TryGetValue(name, out var renderTarget))
        //    {
        //        tex2D = rpc._GetTexCubeByName(visualChannel, name);
        //    }
        //    else
        //    {
        //        name = passSetting.GetAliases(name);

        //        tex2D = rpc._GetTexCubeByName(visualChannel, name);
        //    }
        //    return tex2D;
        //}


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
            if (texture == null) return error;
            return texture.Status switch
            {
                GraphicsObjectStatus.loaded => texture,
                GraphicsObjectStatus.loading => loading,
                GraphicsObjectStatus.unload => unload,
                _ => error
            };
        }


        internal MMDRendererComponent renderer;

        internal Mesh GetMesh(string path) => rpc.mainCaches.GetModel(path).GetMesh();

        internal ResourceWrap.ModelPack GetModel(string path) => rpc.mainCaches.GetModel(path);

        public IEnumerable<MeshRenderable> MeshRenderables(bool setMesh = true)
        {
            var drp = rpc.dynamicContextRead;
            foreach (var renderer in drp.renderers)
            {
                var model = GetModel(renderer.meshPath);
                var mesh = model.GetMesh();
                var meshOverride = rpc.meshOverride[renderer];
                if (setMesh)
                    graphicsContext.SetMesh(mesh, meshOverride);
                this.renderer = renderer;
                for (int i = 0; i < renderer.Materials.Count; i++)
                {
                    var material = renderer.Materials[i];
                    var submesh = model.Submeshes[i];
                    var renderable = new MeshRenderable()
                    {
                        mesh = mesh,
                        meshOverride = meshOverride,
                        transform = renderer.LocalToWorld,
                        gpuSkinning = renderer.skinning && !drp.CPUSkinning,
                    };
                    renderable.material = material;
                    WriteRenderable1(ref renderable, submesh);
                    yield return renderable;
                }
            }
            this.renderer = null;
            foreach (var renderer in drp.meshRenderers)
            {
                var model = GetModel(renderer.meshPath);
                var mesh = model.GetMesh();
                if (setMesh)
                    graphicsContext.SetMesh(mesh);
                for (int i = 0; i < renderer.Materials.Count; i++)
                {
                    var material = renderer.Materials[i];
                    var submesh = model.Submeshes[i];
                    var renderable = new MeshRenderable()
                    {
                        mesh = mesh,
                        meshOverride = null,
                        transform = renderer.transform.GetMatrix(),
                        gpuSkinning = false,
                    };
                    renderable.material = material;
                    WriteRenderable1(ref renderable, submesh);
                    yield return renderable;
                }
            }
            this.renderer = null;
        }

        public void PushParameters(object parameterProvider)
        {
            var type = parameterProvider.GetType();

            if (memberInfoCache.TryGetValue(type, out var parameters))
            {

            }
            else
            {
                parameters = new();
                foreach (var member in type.GetMembers())
                {
                    var indexableAttribute = member.GetCustomAttribute<IndexableAttribute>();
                    if (indexableAttribute != null)
                    {
                        parameters[indexableAttribute.Name ?? member.Name] = member;
                    }
                }
                memberInfoCache[type] = parameters;
            }

            dataStack.Add((parameterProvider, parameters));
        }

        public void PopParameters()
        {
            dataStack.RemoveAt(dataStack.Count - 1);
        }

        void WriteRenderable1(ref MeshRenderable renderable, Submesh submesh)
        {
            renderable.indexStart = submesh.indexOffset;
            renderable.indexCount = submesh.indexCount;
            renderable.vertexStart = submesh.vertexStart;
            renderable.vertexCount = submesh.vertexCount;
            renderable.drawDoubleFace = submesh.DrawDoubleFace;
        }

        public CBuffer GetBoneBuffer()
        {
            return rpc.GetBoneBuffer(renderer);
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
            graphicsContext.SetCBVRSlot(cBuffer, 0, 0, slot);
        }

        public void SetRootSignature(string rs)
        {
            rootSignature = rpc.mainCaches.GetRootSignature(rs);
            graphicsContext.SetRootSignature(rootSignature);
        }

        public object GetIndexableValue(string key)
        {
            if (RenderPipelineView.indexables.TryGetValue(key, out var indexable))
            {
                return indexable.GetValue<object>(RenderPipelineView.renderPipeline);
            }
            return null;
        }

        public object GetIndexableValue(string key, RenderMaterial material)
        {
            if (RenderPipelineView.indexables.TryGetValue(key, out var memberInfo))
            {
                var type = memberInfo.GetGetterType();
                if (material != null && material.Parameters.TryGetValue(key, out object obj1))
                {
                    var objType = obj1.GetType();
                    if (objType == type)
                        return obj1;
                    else if (type.IsEnum && objType == typeof(string))
                    {
                        if (Enum.TryParse(type, (string)obj1, out var obj2))
                        {
                            return obj2;
                        }
                    }
                }
                return memberInfo.GetValue<object>(RenderPipelineView.renderPipeline);
            }
            return null;
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
            dataStack.Clear();
        }

        public void Write(IReadOnlyList<object> datas, GPUWriter writer, RenderMaterial material = null)
        {
            foreach (var obj in datas)
                WriteObject(obj, writer, material);
        }

        public void Write(object[] datas, GPUWriter writer, RenderMaterial material = null)
        {
            foreach (var obj in datas)
                WriteObject(obj, writer, material);
        }

        public void Write(Span<object> datas, GPUWriter writer, RenderMaterial material = null)
        {
            foreach (var obj in datas)
                WriteObject(obj, writer, material);
        }

        public GPUWriter Writer { get => rpc.gpuWriter; }

        void WriteObject(object obj, GPUWriter writer, RenderMaterial material = null)
        {
            var indexable = RenderPipelineView.indexables;
            var pipeline = RenderPipelineView.renderPipeline;
            if (obj is string s)
            {
                bool t0 = false;
                for (int i = dataStack.Count - 1; i >= 0; i--)
                {
                    var finder = dataStack[i];
                    if (finder.Item2.TryGetValue(s, out var memberInfo1))
                    {
                        writer.WriteObject(memberInfo1.GetValue<object>(finder.Item1));
                        t0 = true;
                        break;
                    }
                }
                if (!t0 && indexable.TryGetValue(s, out var memberInfo))
                {
                    var type = memberInfo.GetGetterType();
                    if (material != null && material.Parameters.TryGetValue(s, out object obj1) &&
                        obj1.GetType() == type)
                    {
                        writer.WriteObject(obj1);
                    }
                    else
                    {
                        writer.WriteObject(memberInfo.GetValue<object>(pipeline));
                    }
                }
            }
            else if (obj is Func<object> function)
            {
                writer.WriteObject(function());
            }
            else if (obj is MemberInfo memberInfo)
            {
                writer.WriteObject(memberInfo.GetValue<object>(pipeline));
            }
            else
            {
                writer.WriteObject(obj);
            }
        }

        public void Swap(string renderTexture1, string renderTexture2)
        {
            var rts = RenderPipelineView.RenderTextures;
            rts.TryGetValue(renderTexture1, out var rt1);
            rts.TryGetValue(renderTexture2, out var rt2);
            if (rt1.width == rt2.width && rt1.height == rt2.height && rt1.resourceFormat == rt2.resourceFormat
                && rt1.depth == rt2.depth && rt1.mips == rt2.mips)
            {
                if (rt1.texture2D != null && rt2.texture2D != null)
                {
                    (rt1.texture2D, rt2.texture2D) = (rt2.texture2D, rt1.texture2D);
                    rt1.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt1.texture2D);
                    rt2.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt2.texture2D);
                }
                else if (rt1.textureCube != null && rt2.textureCube != null)
                {
                    (rt1.textureCube, rt2.textureCube) = (rt2.textureCube, rt1.textureCube);
                    rt1.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt1.textureCube);
                    rt2.fieldInfo.SetValue(RenderPipelineView.renderPipeline, rt2.textureCube);
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
