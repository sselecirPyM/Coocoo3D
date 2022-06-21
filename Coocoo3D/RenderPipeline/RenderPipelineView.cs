using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Caprice.Attributes;
using Coocoo3DGraphics;
using System.IO;
using Coocoo3D.Utility;
using Caprice.Display;

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

        internal Dictionary<AOVType, Texture2D> AOVs = new Dictionary<AOVType, Texture2D>();
        internal Dictionary<string, RenderTextureUsage> RenderTextures = new Dictionary<string, RenderTextureUsage>();
        internal Dictionary<Texture2D, string> invertFinding = new Dictionary<Texture2D, string>();

        internal Dictionary<string, string> textureReplacement = new Dictionary<string, string>();
        internal Dictionary<string, MemberInfo> indexables = new Dictionary<string, MemberInfo>();
        internal Dictionary<string, MemberInfo> sceneCaptures = new Dictionary<string, MemberInfo>();

        internal Dictionary<string, UIUsage> UIUsages = new Dictionary<string, UIUsage>();

        internal Dictionary<string, List<string>> dependents = new Dictionary<string, List<string>>();

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
                        sceneCaptures[field.Name] = field;
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

            var members = type.GetMembers();
            foreach (var member in members)
            {
                if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                {
                    _Member(member);
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
                invertFinding[texture2D] = field.Name;
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

        void _Member(MemberInfo member)
        {
            var indexableAttribute = member.GetCustomAttribute<IndexableAttribute>();
            if (indexableAttribute != null)
            {
                indexables[indexableAttribute.Name ?? member.Name] = member;
            }

            var uiShowAttribute = member.GetCustomAttribute<UIShowAttribute>();
            var uiDescriptionAttribute = member.GetCustomAttribute<UIDescriptionAttribute>();
            if (uiShowAttribute != null)
            {
                var usage = new UIUsage()
                {
                    Name = uiShowAttribute.Name,
                    UIShowType = uiShowAttribute.Type,
                    sliderAttribute = member.GetCustomAttribute<UISliderAttribute>(),
                    colorAttribute = member.GetCustomAttribute<UIColorAttribute>(),
                    dragFloatAttribute = member.GetCustomAttribute<UIDragFloatAttribute>(),
                    dragIntAttribute = member.GetCustomAttribute<UIDragIntAttribute>(),
                    MemberInfo = member,
                };
                UIUsages[member.Name] = usage;
                if (uiDescriptionAttribute != null)
                {
                    usage.Description = uiDescriptionAttribute.Description;
                }
                usage.Name ??= member.Name;
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
                    RPUtil.Texture2D(texture2D, (Vortice.DXGI.Format)rt.resourceFormat, rt.width, rt.height, rt.mips, graphicsContext);

                if (textureCube != null && rt.resourceFormat != ResourceFormat.Unknown && rt.width != 0 && rt.height != 0)
                    RPUtil.TextureCube(textureCube, (Vortice.DXGI.Format)rt.resourceFormat, rt.width, rt.height, rt.mips, graphicsContext);

                if (gpuBuffer != null && rt.width != 0)
                    RPUtil.DynamicBuffer(gpuBuffer, rt.width, graphicsContext);

            }
            foreach (var rt in RenderTextures.Values)
            {
                if (rt.autoClearAttribute != null)
                {
                    var c = rt.autoClearAttribute;
                    if (rt.texture2D != null)
                    {
                        graphicsContext.ClearTexture(rt.texture2D, new System.Numerics.Vector4(c.R, c.G, c.B, c.A), c.Depth);
                    }
                }
            }
            foreach (var rt in RenderTextures.Values)
                Bake(rt);
        }

        void Bake(RenderTextureUsage rt)
        {
            if (rt.baked || rt.runtimeBakeAttribute == null) return;

            if (rt.bakeDependencyAttribute != null)
            {
                foreach (var dep in rt.bakeDependencyAttribute.dependencies)
                {
                    if (!RenderTextures.TryGetValue(dep, out var rt2)) return;
                    if (rt2.runtimeBakeAttribute != null && rt2.baked == false) return;
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

        public void Export(Dictionary<string, object> settings)
        {
            foreach (var uiUsage in UIUsages)
            {
                if (uiUsage.Value.UIShowType != UIShowType.Global && uiUsage.Value.UIShowType != UIShowType.All)
                    continue;
                object obj = uiUsage.Value.MemberInfo.GetValue<object>(renderPipeline);
                if (uiUsage.Value.MemberInfo.GetGetterType() == typeof(Texture2D))
                {
                    if (textureReplacement.TryGetValue(uiUsage.Key, out var rt))
                        settings[uiUsage.Key] = rt;
                }
                else
                    settings[uiUsage.Key] = obj;
            }
        }

        public void Import(Dictionary<string, object> settings)
        {
            foreach (var uiUsage in UIUsages)
            {
                if (settings.TryGetValue(uiUsage.Key, out object value))
                {
                    var type = uiUsage.Value.MemberInfo.GetGetterType();
                    if (type == typeof(Texture2D))
                    {
                        if (value is string s)
                        {
                            textureReplacement[uiUsage.Key] = s;
                        }
                    }
                    else if (type == value.GetType())
                        uiUsage.Value.MemberInfo.SetValue(renderPipeline, value);
                }
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
            }
        }
    }
}
