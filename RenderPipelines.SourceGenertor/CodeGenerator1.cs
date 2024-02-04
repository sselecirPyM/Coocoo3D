using System.Collections.Generic;
using System.Linq;

namespace RenderPipelines.SourceGenertor
{
    public class CodeGenerator1
    {
        public List<Component> components;

        public string codeNamespace = "RenderPipelines";

        Dictionary<string, HlslProgram> hlsls = new Dictionary<string, HlslProgram>();

        BindingsMap propertyBindings = new BindingsMap();

        public string GenerateCode()
        {
            foreach (var component in components)
            {
                foreach (var hlsl in component.hlslPrograms)
                {
                    hlsls[hlsl.name] = hlsl;
                }
            }

            FormatStringBuilder sb = new FormatStringBuilder();
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Numerics;");
            sb.AppendLine($"using System.Runtime.InteropServices;");
            sb.AppendLine($"using float4x4 = System.Numerics.Matrix4x4;");
            sb.AppendLine($"using RenderPipelines.Utility;");
            sb.AppendLine($"using Coocoo3DGraphics;");

            foreach (var component in components)
            {
                foreach (var componentUsing in component.usings)
                {
                    sb.AppendLine($"using {componentUsing.value};");
                }
                foreach (var passLike in component.children)
                {
                    if (passLike is PassPlaceHolder placeHolder)
                    {
                        foreach (var binding in placeHolder.bindings)
                        {
                            propertyBindings.Add(binding.Value, $"{passLike.name}.{binding.Key} = value;");
                            //propertyBindings.Add(passLike.name, $"if(value != null)value.{binding.Key} = {binding.Value};");
                        }
                    }
                }
            }
            sb.AppendLine($"namespace {codeNamespace}");
            sb.Open("{");
            foreach (var component in components)
            {
                List<ComponentProperty> properties = new List<ComponentProperty>();
                properties.AddRange(component.properties);
                properties.Add(new ComponentProperty() { name = "context", type = "RenderHelper" });

                void _var(HlslVar hlslVar)
                {
                    if (properties.Any(e => e.name == hlslVar.name))
                    {
                        return;
                    }
                    if (hlslVar.xType != string.Empty)
                        properties.Add(new ComponentProperty()
                        {
                            name = hlslVar.name,
                            type = hlslVar.xType ?? hlslVar.type,
                        });
                }
                foreach (var hlsl in component.hlslPrograms)
                {
                    foreach (var srv in hlsl.srvs)
                        _var(srv);
                    foreach (var uav in hlsl.uavs)
                        _var(uav);
                }

                sb.AppendLine($"public partial class {component.name}");
                sb.Open("{");

                foreach (var passLike in component.children)
                {
                    if (passLike is Render render)
                        GenerateRender(render, sb);
                    else if (passLike is Script script)
                        ComponentScriptChild(script, sb);
                    else if (passLike is TextureProperty textureProperty)
                        ProcessTextureProperty(textureProperty, sb);
                    else if (passLike is PassPlaceHolder passPlaceHolder)
                        ProcessPlaceHolder(passPlaceHolder, sb);
                }

                foreach (var hlsl in component.hlslPrograms)
                {
                    ProcessHlslBindCode(hlsl, sb);
                }
                if (component.generateDispose)
                {
                    sb.AppendLine("public void Dispose()");
                    sb.Open("{");
                    foreach (var hlsl in component.hlslPrograms)
                    {
                        sb.AppendLine($"{hlsl.name}?.Dispose();");
                    }
                    sb.Close("}");
                }

                foreach (var hlsl in component.hlslPrograms)
                {
                    var sb1 = new FormatStringBuilder();
                    var result = GenerateHlsl(hlsl, sb1);
                    if (hlsl.compute == null)
                    {
                        sb.AppendLine($"VariantShader {hlsl.name} = new VariantShader(\"\"\"");
                        sb.Append(result.code);
                        sb.Append($"\"\"\", {hlsl.vertex}, {hlsl.geometry ?? "null"}, {hlsl.pixel ?? "null"});");
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine($"VariantComputeShader {hlsl.name} = new VariantComputeShader(\"\"\"");
                        sb.Append(result.code);
                        sb.Append($"\"\"\", {hlsl.compute}, {hlsl.fileName ?? "null"});");
                        sb.AppendLine();
                    }
                }

                foreach (var property in properties)
                {
                    sb.AppendLine($"public {property.type} {property.name};");
                }

                sb.Close("}");
            }
            sb.Close("}");
            return sb.ToString();
        }

        static void ComponentScriptChild(Script script, FormatStringBuilder sb)
        {
            sb.AppendLine(script.source);
            foreach (var passLike in script.x_children)
            {
                if (passLike is Script script2)
                    ComponentScriptChild(script2, sb);
            }
        }

        void ProcessTextureProperty(TextureProperty textureProperty, FormatStringBuilder sb)
        {
            if (textureProperty.aov != null)
                sb.AppendLine($"[AOV(AOVType.{textureProperty.aov})]");
            if (textureProperty.size != null)
                sb.AppendLine($"[Size({textureProperty.size})]");
            if (textureProperty.format != null)
                sb.AppendLine($"[Format(ResourceFormat.{textureProperty.format})]");
            if (textureProperty.autoClear)
                sb.AppendLine($"[AutoClear]");

            WritePorpertyBinding("Texture2D", textureProperty.name, textureProperty.x_attributes, sb, false);
        }

        void ProcessPlaceHolder(PassPlaceHolder placeHolder, FormatStringBuilder sb)
        {
            if (!placeHolder.generateCode)
            {
                return;
            }

            WritePorpertyBinding(placeHolder.placeHolderType, placeHolder.name, placeHolder.x_attributes, sb, true);
        }

        void WritePorpertyBinding(string type, string name, IEnumerable<KeyValuePair<string, string>> attributes, FormatStringBuilder sb, bool construct)
        {
            foreach (var attr in attributes)
            {
                sb.AppendLine($"[{attr.Key}({attr.Value})]");
            }
            sb.AppendLine($"public {type} {name}");
            sb.Open("{");
            sb.AppendLine($"get => x_{name};");
            sb.AppendLine($"set");
            sb.Open("{");
            sb.AppendLine($"x_{name} = value;");
            if (propertyBindings.TryGetValue(name, out var list))
            {
                foreach (var binding in list)
                {
                    sb.AppendLine($"{binding}");
                }
            }
            sb.Close("}");
            sb.Close("}");
            if (construct)
                sb.AppendLine($"{type} x_{name} = new();");
            else
                sb.AppendLine($"{type} x_{name};");
            sb.AppendLine();
        }

        static void GenerateRender(Render render, FormatStringBuilder sb)
        {
            sb.AppendLine($"public void {render.name}({render.parameter})");
            sb.Open("{");
            foreach (var passLike in render.x_children)
            {
                ProcessPassLikeFunction(passLike, sb);
            }
            sb.Close("}");
        }

        static void ProcessPassLikeFunction(PassLike passLike, FormatStringBuilder sb)
        {
            bool bracket = false;
            if (passLike.directives.TryGetValue("foreach", out var foreachval))
            {
                bracket = true;
                sb.AppendLine($"foreach(var {foreachval})");
            }
            else if (passLike.directives.TryGetValue("for", out var forval))
            {
                bracket = true;
                sb.AppendLine($"for({forval})");
            }
            if (passLike.directives.TryGetValue("if", out var ifval))
            {
                bracket = true;
                sb.AppendLine($"if({ifval})");
            }
            if (passLike.directives.TryGetValue("else-if", out var elseifval))
            {
                bracket = true;
                sb.AppendLine($"else if({elseifval})");
            }
            if (passLike.directives.TryGetValue("else", out _))
            {
                bracket = true;
                sb.AppendLine($"else");
            }

            if (bracket)
                sb.Open("{");
            switch (passLike)
            {
                case DrawCall drawCall:
                    switch (drawCall.type)
                    {
                        case "quad":
                            sb.AppendLine($"context.DrawQuad();");
                            break;
                        default:
                            sb.AppendLine($"context.DrawIndexedInstanced({drawCall.indexCount}, 1, {drawCall.indexStart}, 0, 0);");
                            break;
                    }
                    break;
                case DispatchCall dispatchCall:
                    sb.AppendLine($"context.Dispatch({dispatchCall.x}, {dispatchCall.y}, {dispatchCall.z});");
                    break;
                case Script script:
                    sb.AppendLine(script.source);
                    break;
                case RenderShader shader:
                    sb.AppendLine($"_bind_{shader.source}();");
                    break;
            }
            foreach (var child in passLike.x_children)
            {
                ProcessPassLikeFunction(child, sb);
            }
            if (bracket)
                sb.Close("}");
        }

        static void ProcessHlslBindCode(HlslProgram hlsl, FormatStringBuilder sb)
        {
            if (hlsl.compute == null)
                ProcessHlslBindRender(hlsl, sb);
            else
                ProcessHlslBindCompute(hlsl, sb);
        }

        static void ProcessHlslBindRender(HlslProgram hlsl, FormatStringBuilder sb)
        {
            sb.AppendLine($"public void _bind_{hlsl.name}()");
            sb.Open("{");
            for (int i = 0; i < hlsl.srvs.Count; i++)
            {
                HlslVar item = hlsl.srvs[i];
                if (item.autoBinding)
                    sb.AppendLine($"context.SetSRV({i}, {item.name});");
            }
            for (int i = 0; i < hlsl.uavs.Count; i++)
            {
                HlslVar item = hlsl.uavs[i];
                if (item.autoBinding)
                    sb.AppendLine($"context.SetUAV({i}, {item.name});");
            }
            sb.AppendLine($"context.SetPSO({hlsl.name}, new PSODesc(){{ cullMode = CullMode.None }});");
            sb.Close("}");
        }

        static void ProcessHlslBindCompute(HlslProgram hlsl, FormatStringBuilder sb)
        {
            sb.AppendLine($"public void _bind_{hlsl.name}()");
            sb.Open("{");
            for (int i = 0; i < hlsl.srvs.Count; i++)
            {
                HlslVar item = hlsl.srvs[i];
                if (item.autoBinding)
                    sb.AppendLine($"context.SetSRV({i}, {item.name});");
            }
            for (int i = 0; i < hlsl.uavs.Count; i++)
            {
                HlslVar item = hlsl.uavs[i];
                if (item.autoBinding)
                    sb.AppendLine($"context.SetUAV({i}, {item.name});");
            }
            sb.AppendLine($"context.SetPSO({hlsl.name});");
            sb.Close("}");
        }

        public GeneratedHlsl[] GenerateHlsls()
        {
            List<GeneratedHlsl> result = new List<GeneratedHlsl>();
            foreach (var component in components)
            {
                FormatStringBuilder sb = new FormatStringBuilder();
                foreach (var hlsl in component.hlslPrograms)
                {
                    sb.Clear();

                    result.Add(GenerateHlsl(hlsl, sb));
                }
            }
            return result.ToArray();
        }

        GeneratedHlsl GenerateHlsl(HlslProgram hlslProgram, FormatStringBuilder sb)
        {
            var generated = new GeneratedHlsl();
            if (hlslProgram.vars.Count > 0)
            {
                sb.AppendLine("cbuffer cb0 : register(b0)");
                sb.Open("{");
                foreach (var v in hlslProgram.vars)
                {
                    sb.AppendLine($"{v.type} {v.name};");
                }
                sb.Close("}");
            }
            int index = 0;
            foreach (var v in hlslProgram.srvs)
            {
                sb.AppendLine($"{v.type} {v.name} : register(t{index});");
                index++;
            }
            index = 0;
            foreach (var v in hlslProgram.uavs)
            {
                sb.AppendLine($"{v.type} {v.name} : register(u{index});");
                index++;
            }
            sb.AppendLine(hlslProgram.code);

            generated.name = hlslProgram.name;
            generated.code = sb.ToString();
            return generated;
        }
    }
}
