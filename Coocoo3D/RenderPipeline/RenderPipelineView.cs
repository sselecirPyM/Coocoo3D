using Caprice.Attributes;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline;

public class RenderPipelineView : IDisposable
{
    public RenderPipeline renderPipeline;
    public MainCaches mainCaches;

    public string path;

    public RenderPipelineView(RenderPipeline renderPipeline, MainCaches mainCaches, string path)
    {
        this.renderPipeline = renderPipeline;
        this.path = path;
        this.mainCaches = mainCaches;
        GetMetaData();
    }

    internal Dictionary<AOVType, Texture2D> AOVs = new();
    public Dictionary<string, RenderTextureUsage> RenderTextures = new();
    internal HashSet<Texture2D> internalTextures = new();

    internal Dictionary<string, (MemberInfo, SceneCaptureAttribute)> sceneCaptures = new();

    internal Dictionary<string, List<string>> dependents = new();

    internal RenderWrap renderWrap;

    List<RenderTextureUsage> bakes = new();

    void GetMetaData()
    {
        var type = renderPipeline.GetType();

        foreach (var memberInfo in type.GetMembers())
        {
            if (!(memberInfo is PropertyInfo || memberInfo is FieldInfo))
            {
                continue;
            }
            var type2 = memberInfo.GetGetterType();
            if (type2 == typeof(Texture2D) || type2 == typeof(GPUBuffer))
            {
                RenderResource(memberInfo);
            }
            var sceneCapture = memberInfo.GetCustomAttribute<SceneCaptureAttribute>();
            if (sceneCapture != null)
            {
                sceneCaptures[memberInfo.Name] = (memberInfo, sceneCapture);
            }
        }

        bool _iteration = true;
        while (_iteration)
        {
            _iteration = false;
            foreach (var dep in dependents)
            {
                for (int i = 0; i < dep.Value.Count; i++)
                {
                    string dep1 = dep.Value[i];
                    int count1 = dep.Value.Count;
                    if (dependents.TryGetValue(dep1, out var dep2))
                    {
                        dep.Value.AddRange(dep2.Except(dep.Value));
                    }
                    if (count1 != dep.Value.Count)
                        _iteration = true;
                }
            }
        }
    }

    void RenderResource(MemberInfo member)
    {
        var aovAttribute = member.GetCustomAttribute<AOVAttribute>();
        var autoClearAttribute = member.GetCustomAttribute<AutoClearAttribute>();
        var formatAttribute = member.GetCustomAttribute<FormatAttribute>();
        var resourceAttribute = member.GetCustomAttribute<ResourceAttribute>();
        var runtimeBakeAttribute = member.GetCustomAttribute<RuntimeBakeAttribute>();
        var sizeAttribute = member.GetCustomAttribute<SizeAttribute>();
        var bakeDependencyAttribute = member.GetCustomAttribute<BakeDependencyAttribute>();

        var rt = new RenderTextureUsage
        {
            sizeAttribute = sizeAttribute,
            autoClearAttribute = autoClearAttribute,
            formatAttribute = formatAttribute,
            runtimeBakeAttribute = runtimeBakeAttribute,
            bakeDependency = bakeDependencyAttribute?.dependencies,
            name = member.Name,
            memberInfo = member,
            bindingObject = renderPipeline
        };
        RenderTextures[member.Name] = rt;
        if (runtimeBakeAttribute != null)
        {
            bakes.Add(rt);
        }

        if (rt.bakeDependency != null)
        {
            foreach (var dep in rt.bakeDependency)
            {
                if (!dependents.TryGetValue(dep, out var list))
                {
                    list = new List<string>();
                    dependents[dep] = list;
                }
                list.Add(member.Name);
            }
        }
        if (sizeAttribute != null)
        {
            rt.width = sizeAttribute.X;
            rt.height = sizeAttribute.Y;
            rt.mips = sizeAttribute.Mips;
            rt.arraySize = sizeAttribute.ArraySize;
        }
        if (formatAttribute != null)
        {
            rt.resourceFormat = formatAttribute.format;
        }

        if (member.GetGetterType() == typeof(Texture2D))
        {
            if (resourceAttribute != null)
            {
                string fullPath = Path.GetFullPath(resourceAttribute.Resource, path);
                var tex = mainCaches.GetTexturePreloaded(fullPath);
                member.SetValue(renderPipeline, tex);
            }
            else
            {
                Texture2D texture2D = new Texture2D();
                texture2D.Name = member.Name;
                internalTextures.Add(texture2D);
                if (aovAttribute != null)
                {
                    if (AOVs.ContainsKey(aovAttribute.AOVType))
                        Console.WriteLine(member.Name + ". Duplicate AOV bindings.");

                    AOVs[aovAttribute.AOVType] = texture2D;
                }
                member.SetValue(renderPipeline, texture2D);
            }
        }
        if (member.GetGetterType() == typeof(GPUBuffer))
        {
            GPUBuffer buffer = new GPUBuffer();
            buffer.Name = member.Name;
            rt.gpuBuffer = buffer;
            member.SetValue(renderPipeline, buffer);
        }
    }

    internal void InvalidDependents(string key)
    {
        if (dependents.TryGetValue(key, out var dep1))
            foreach (var dep in dep1)
            {
                var rt = RenderTextures[dep];
                rt.ready = false;
                rt.bakeTag = null;
            }
    }

    internal void PrepareRenderResources()
    {
        var graphicsContext = renderWrap.graphicsContext;
        foreach (var rt in RenderTextures.Values)
        {
            var texture2d = rt.GetTexture2D();
            var gpuBuffer = rt.gpuBuffer;

            if (texture2d != null && rt.resourceFormat != ResourceFormat.Unknown && rt.width != 0 && rt.height != 0)
                Texture2D(texture2d, (Vortice.DXGI.Format)rt.resourceFormat, rt.width, rt.height, rt.mips, rt.arraySize, graphicsContext);

            if (gpuBuffer != null && rt.width != 0)
                DynamicBuffer(gpuBuffer, rt.width, graphicsContext);

        }
        foreach (var rt in RenderTextures.Values)
        {
            var c = rt.autoClearAttribute;
            if (c == null)
                continue;
            var texture2d = rt.GetTexture2D();
            if (texture2d != null)
            {
                graphicsContext.ClearTexture(texture2d, new System.Numerics.Vector4(c.R, c.G, c.B, c.A), c.Depth);
            }
        }
        foreach (var rt in bakes)
            Bake(rt);
    }

    void Bake(RenderTextureUsage rt)
    {
        if (rt.ready)
            return;

        if (rt.bakeDependency != null)
        {
            foreach (var dep in rt.bakeDependency)
            {
                if (!RenderTextures.TryGetValue(dep, out var rt2))
                    return;
                if (rt2.runtimeBakeAttribute != null && rt2.ready == false)
                    return;
            }
        }

        if (rt.memberInfo.GetGetterType() == typeof(Texture2D) && rt.runtimeBakeAttribute is ITexture2DBaker baker1)
        {
            rt.ready = baker1.Bake(rt.memberInfo.GetValue2<Texture2D>(renderPipeline), renderWrap, ref rt.bakeTag);
        }
    }

    public Texture2D GetAOV(AOVType type)
    {
        AOVs.TryGetValue(type, out Texture2D texture);
        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in internalTextures)
        {
            texture.Dispose();
        }
        foreach (var rt in RenderTextures)
        {
            rt.Value.gpuBuffer?.Dispose();
        }
        foreach (var bake in bakes)
        {
            if (bake.runtimeBakeAttribute is IDisposable disposable1)
            {
                disposable1.Dispose();
            }
        }
        if (renderPipeline is IDisposable disposable)
            disposable.Dispose();
        RenderTextures = null;
    }

    static void Texture2D(Texture2D tex2d, Format format, int x, int y, int mips, int arraySize, GraphicsContext graphicsContext)
    {
        if (tex2d.width != x || tex2d.height != y || tex2d.mipLevels != mips || tex2d.GetFormat() != format)
        {
            if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                tex2d.ReloadAsDSV(x, y, mips, format);
            else
                tex2d.ReloadAsRTVUAV(x, y, mips, arraySize, format);
            graphicsContext.UpdateRenderTexture(tex2d);
        }
    }

    static void DynamicBuffer(GPUBuffer dynamicBuffer, int width, GraphicsContext graphicsContext)
    {
        if (width != dynamicBuffer.size)
        {
            dynamicBuffer.size = width;
            graphicsContext.UpdateDynamicBuffer(dynamicBuffer);
        }
    }
    public class Texture2DTransfer
    {
        public Texture2D texture2D;
        public Texture2DTransfer(Texture2D texture2D)
        {
            this.texture2D = texture2D;
        }
        public static implicit operator Texture2D(Texture2DTransfer texture2DTransfer)
        {
            return texture2DTransfer.texture2D;
        }
    }
}
