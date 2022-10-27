using Caprice.Attributes;
using Caprice.Display;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class RenderPipelineView : IDisposable
    {
        public RenderPipeline renderPipeline;

        public string path;

        public RenderPipelineView(RenderPipeline renderPipeline, string path)
        {
            this.renderPipeline = renderPipeline;
            this.path = path;
            GetMetaData();
        }

        internal Dictionary<AOVType, Texture2D> AOVs = new();
        internal Dictionary<string, RenderTextureUsage> RenderTextures = new();

        internal Dictionary<string, string> textureReplacement = new();
        internal Dictionary<string, (MemberInfo, SceneCaptureAttribute)> sceneCaptures = new();

        internal Dictionary<string, List<string>> dependents = new();

        internal RenderWrap renderWrap;

        void GetMetaData()
        {
            var type = renderPipeline.GetType();
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Texture2D) || field.FieldType == typeof(TextureCube) || field.FieldType == typeof(GPUBuffer))
                {
                    RenderResource(field);
                }
                else
                {
                    var sceneCapture = field.GetCustomAttribute<SceneCaptureAttribute>();
                    if (sceneCapture != null)
                    {
                        sceneCaptures[field.Name] = (field, sceneCapture);
                    }
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

        void RenderResource(FieldInfo field)
        {
            var aovAttribute = field.GetCustomAttribute<AOVAttribute>();
            var clearColorAttribute = field.GetCustomAttribute<AutoClearAttribute>();
            var formatAttribute = field.GetCustomAttribute<FormatAttribute>();
            var resourceAttribute = field.GetCustomAttribute<ResourceAttribute>();
            var runtimeBakeAttribute = field.GetCustomAttribute<RuntimeBakeAttribute>();
            var sizeAttribute = field.GetCustomAttribute<SizeAttribute>();
            var srgbAttribute = field.GetCustomAttribute<SrgbAttribute>();
            var bakeDependencyAttribute = field.GetCustomAttribute<BakeDependencyAttribute>();

            var rt = new RenderTextureUsage
            {
                sizeAttribute = sizeAttribute,
                autoClearAttribute = clearColorAttribute,
                formatAttribute = formatAttribute,
                runtimeBakeAttribute = runtimeBakeAttribute,
                bakeDependencyAttribute = bakeDependencyAttribute,
                srgbAttribute = srgbAttribute,
                name = field.Name,
                fieldInfo = field,
            };
            RenderTextures[field.Name] = rt;

            if (bakeDependencyAttribute != null)
            {
                foreach (var dep in bakeDependencyAttribute.dependencies)
                {
                    if (!dependents.TryGetValue(dep, out var list))
                    {
                        list = new List<string>();
                        dependents[dep] = list;
                    }
                    list.Add(field.Name);
                }
            }
            if (resourceAttribute != null)
            {
                textureReplacement[field.Name] = Path.GetFullPath(resourceAttribute.Resource, path);
            }
            if (sizeAttribute != null)
            {
                rt.width = sizeAttribute.X;
                rt.height = sizeAttribute.Y;
                rt.mips = sizeAttribute.Mips;
            }
            if (formatAttribute != null)
            {
                rt.resourceFormat = formatAttribute.format;
            }

            if (field.FieldType == typeof(Texture2D))
            {
                Texture2D texture2D = new Texture2D();
                texture2D.Name = field.Name;
                rt.texture2D = texture2D;

                if (aovAttribute != null)
                {
                    if (AOVs.ContainsKey(aovAttribute.AOVType))
                        Console.WriteLine(field.Name + ". Duplicate AOV bindings.");

                    AOVs[aovAttribute.AOVType] = texture2D;
                }
                field.SetValue(renderPipeline, texture2D);
            }
            if (field.FieldType == typeof(TextureCube))
            {
                TextureCube textureCube = new TextureCube();
                textureCube.Name = field.Name;
                rt.textureCube = textureCube;
                field.SetValue(renderPipeline, textureCube);
            }
            if (field.FieldType == typeof(GPUBuffer))
            {
                GPUBuffer buffer = new GPUBuffer();
                buffer.Name = field.Name;
                rt.gpuBuffer = buffer;
                field.SetValue(renderPipeline, buffer);
            }
        }

        internal void SetReplacement(string key, string path)
        {
            textureReplacement[key] = path;
        }

        internal void InvalidDependents(string key)
        {
            if (dependents.TryGetValue(key, out var dep1))
                foreach (var dep in dep1)
                {
                    var rt = RenderTextures[dep];
                    rt.baked = false;
                    rt.bakeTag = null;
                }
        }

        internal void PrepareRenderResources()
        {
            var graphicsContext = renderWrap.graphicsContext;
            foreach (var rt in RenderTextures.Values)
            {
                var texture2D = rt.texture2D;
                var textureCube = rt.textureCube;
                var gpuBuffer = rt.gpuBuffer;
                if (texture2D != null && rt.resourceFormat != ResourceFormat.Unknown && rt.width != 0 && rt.height != 0)
                    Texture2D(texture2D, (Vortice.DXGI.Format)rt.resourceFormat, rt.width, rt.height, rt.mips, graphicsContext);

                if (textureCube != null && rt.resourceFormat != ResourceFormat.Unknown && rt.width != 0 && rt.height != 0)
                    TextureCube(textureCube, (Vortice.DXGI.Format)rt.resourceFormat, rt.width, rt.height, rt.mips, graphicsContext);

                if (gpuBuffer != null && rt.width != 0)
                    DynamicBuffer(gpuBuffer, rt.width, graphicsContext);

            }
            foreach (var rt in RenderTextures.Values)
            {
                var c = rt.autoClearAttribute;
                if (c == null)
                    continue;
                if (rt.texture2D != null)
                {
                    graphicsContext.ClearTexture(rt.texture2D, new System.Numerics.Vector4(c.R, c.G, c.B, c.A), c.Depth);
                }
            }
            foreach (var rt in RenderTextures.Values)
                Bake(rt);
        }

        void Bake(RenderTextureUsage rt)
        {
            if (rt.baked || rt.runtimeBakeAttribute == null)
                return;

            if (rt.bakeDependencyAttribute != null)
            {
                foreach (var dep in rt.bakeDependencyAttribute.dependencies)
                {
                    if (!RenderTextures.TryGetValue(dep, out var rt2))
                        return;
                    if (rt2.runtimeBakeAttribute != null && rt2.baked == false)
                        return;
                }
            }

            if (rt.texture2D != null && rt.runtimeBakeAttribute is ITexture2DBaker baker1)
            {
                rt.baked = baker1.Bake(rt.texture2D, renderWrap, ref rt.bakeTag);
            }
            else if (rt.textureCube != null && rt.runtimeBakeAttribute is ITextureCubeBaker baker2)
            {
                rt.baked = baker2.Bake(rt.textureCube, renderWrap, ref rt.bakeTag);
            }
        }

        public void Export(Dictionary<string, object> settings, List<UIUsage> UIUsages)
        {
            foreach (var usage in UIUsages)
            {
                if (usage.UIShowType != UIShowType.Global && usage.UIShowType != UIShowType.All)
                    continue;
                object obj = usage.MemberInfo.GetValue<object>(renderPipeline);
                if (usage.MemberInfo.GetGetterType() == typeof(Texture2D))
                {
                    if (textureReplacement.TryGetValue(usage.MemberInfo.Name, out var rt))
                        settings[usage.MemberInfo.Name] = rt;
                }
                else
                    settings[usage.MemberInfo.Name] = obj;
            }
        }

        public void Import(Dictionary<string, object> settings, List<UIUsage> UIUsages)
        {
            foreach (var usage in UIUsages)
            {
                if (!settings.TryGetValue(usage.MemberInfo.Name, out object settingValue))
                    continue;
                var type = usage.MemberInfo.GetGetterType();
                if (type == typeof(Texture2D))
                {
                    if (settingValue is string s)
                    {
                        textureReplacement[usage.MemberInfo.Name] = s;
                    }
                }
                else if (type == settingValue.GetType())
                    usage.MemberInfo.SetValue(renderPipeline, settingValue);
            }
        }

        public Texture2D GetAOV(AOVType type)
        {
            AOVs.TryGetValue(type, out Texture2D texture);
            return texture;
        }

        public void Dispose()
        {
            foreach (var rt in RenderTextures)
            {
                rt.Value.texture2D?.Dispose();
                rt.Value.textureCube?.Dispose();
                rt.Value.gpuBuffer?.Dispose();
            }
            RenderTextures = null;
        }

        static void Texture2D(Texture2D tex2d, Format format, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (tex2d.width != x || tex2d.height != y || tex2d.mipLevels != mips || tex2d.GetFormat() != format)
            {
                if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                    tex2d.ReloadAsDSV(x, y, mips, format);
                else
                    tex2d.ReloadAsRTVUAV(x, y, mips, format);
                graphicsContext.UpdateRenderTexture(tex2d);
            }
        }

        static void TextureCube(TextureCube texCube, Format format, int x, int y, int mips, GraphicsContext graphicsContext)
        {
            if (texCube.width != x || texCube.height != y || texCube.mipLevels != mips || texCube.GetFormat() != format)
            {
                if (format == Format.D16_UNorm || format == Format.D24_UNorm_S8_UInt || format == Format.D32_Float)
                    texCube.ReloadAsDSV(x, y, mips, format);
                else
                    texCube.ReloadAsRTVUAV(x, y, mips, format);
                graphicsContext.UpdateRenderTexture(texCube);
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
    }
}
