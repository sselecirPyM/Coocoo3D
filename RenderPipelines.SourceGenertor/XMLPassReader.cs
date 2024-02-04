using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace RenderPipelines.SourceGenertor
{
    public class XMLPassReader
    {
        public void ReadStream(Stream stream)
        {
            XmlReader reader = XmlReader.Create(stream);
            Read(reader);
        }

        public List<Component> components = new List<Component>();

        Stack<object> state = new Stack<object>();
        int depth = -1;

        void Read(XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.Name == "xml")
                    continue;
                if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment)
                    continue;

                //switch (reader.NodeType)
                //{
                //    case XmlNodeType.CDATA:
                //    case XmlNodeType.Text:
                //        Console.WriteLine("{0},{1},{2},{3}", reader.Depth, reader.NodeType, reader.AttributeCount, reader.Value);
                //        break;
                //    default:
                //        Console.WriteLine("{0},{1},{2},{3}", reader.Name, reader.Depth, reader.NodeType, reader.AttributeCount);
                //        break;
                //}
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        ReadElement(reader);
                        break;
                    case XmlNodeType.EndElement:
                        for (int i = 0; i < depth - reader.Depth + 1; i++)
                            EndElement();
                        depth = reader.Depth - 1;
                        break;
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        ReadText(reader);
                        break;
                }
            }
        }

        Dictionary<string, Func<PassLike>> onlyLink = new Dictionary<string, Func<PassLike>>()
        {
            { "pass",()=>new Pass() },
            { "render",()=>new Render() },
            { "copy",()=>new CopyPass() },
            { "draw",()=>new DrawCall() },
            { "script",()=>new Script() },
            { "dispatch",()=>new DispatchCall() },
            { "texture",()=>new TextureProperty() },
        };

        void ReadElement(XmlReader reader)
        {
            if (depth == reader.Depth)
            {
                EndElement();
            }
            depth = reader.Depth;
            var parent = state.Count > 0 ? state.Peek() : null;
            switch (reader.Name)
            {
                case "hlsl":
                    {
                        var hlsl = new HlslProgram();
                        state.Push(hlsl);
                        switch (parent)
                        {
                            case Component component:
                                component.hlslPrograms.Add(hlsl);
                                break;
                        }

                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(hlsl, reader);
                        }
                    }
                    break;
                case "var":
                case "srv":
                case "uav":
                    state.Push(reader.Name);
                    switch (parent)
                    {
                        case HlslProgram hlslProgram:
                            {
                                var hlslvar = new HlslVar();
                                if (reader.Name == "var")
                                    hlslProgram.vars.Add(hlslvar);
                                if (reader.Name == "srv")
                                    hlslProgram.srvs.Add(hlslvar);
                                if (reader.Name == "uav")
                                    hlslProgram.uavs.Add(hlslvar);

                                while (reader.MoveToNextAttribute())
                                {
                                    BindAttribute(hlslvar, reader);
                                }
                            }
                            break;
                        case Render render:
                            {
                                var hlslvar = new HlslVar();
                                //render.vars.Add(hlslvar);
                                if (reader.Name == "var")
                                    render.vars.Add(hlslvar);
                                if (reader.Name == "srv")
                                    render.srvs.Add(hlslvar);
                                if (reader.Name == "uav")
                                    render.uavs.Add(hlslvar);

                                while (reader.MoveToNextAttribute())
                                {
                                    BindAttribute(hlslvar, reader);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case "sampler":
                    state.Push(reader.Name);
                    switch (parent)
                    {
                        case HlslProgram hlslProgram:
                            {
                                var hlslSampler = new HlslSampler();
                                hlslProgram.samplers.Add(hlslSampler);

                                while (reader.MoveToNextAttribute())
                                {
                                    BindAttribute(hlslSampler, reader);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case "variants":
                    {
                        var collection = new ShaderVariantCollection();
                        state.Push(collection);
                        switch (parent)
                        {
                            case Component component:
                                component.variantCollections.Add(collection);
                                break;
                            default:
                                break;
                        }
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(collection, reader);
                        }
                    }
                    break;
                case "variant":
                    {
                        var variant = new ShaderVariant();
                        state.Push(variant);
                        switch (parent)
                        {
                            case ShaderVariantCollection collection:
                                collection.variants.Add(variant);
                                break;
                            default:
                                break;
                        }
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(variant, reader);
                        }
                    }
                    break;
                case "using":
                    {
                        var componentUsing = new ComponentUsing();
                        state.Push(componentUsing);
                        switch (parent)
                        {
                            case Component component:
                                component.usings.Add(componentUsing);
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case "code":
                    state.Push(parent);
                    switch (parent)
                    {
                        case HlslProgram hlslProgram:
                            while (reader.MoveToNextAttribute())
                            {
                            }
                            break;
                        default:
                            break;
                    }

                    break;
                case "shader":
                    {
                        var shader = new RenderShader();
                        state.Push(shader);
                        switch (parent)
                        {
                            case Render render:
                                render.x_children.Add(shader);
                                break;
                        }
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(shader, reader);
                        }
                    }
                    break;
                case "blend":
                    {
                        var blend = new Blend();
                        state.Push(blend);
                        switch (parent)
                        {
                            case Render render:
                                render.blend = blend;
                                break;
                            default:
                                break;
                        }
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(blend, reader);
                        }
                    }
                    break;
                case "ib":
                case "vb":
                    {
                        var ib = new RenderBufferBind();
                        state.Push(ib);
                        switch (parent)
                        {
                            case Render render:
                                if (reader.Name == "ib")
                                    render.indexBuffer = ib;
                                else if (reader.Name == "vb")
                                    render.vertexBuffers.Add(ib);
                                break;
                        }

                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(ib, reader);
                        }
                    }
                    break;
                case "root":
                    {
                        var component = new Component();
                        state.Push(component);
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute1(component, reader);
                        }
                        if (component.name != null)
                            components.Add(component);
                    }
                    break;
                case "property":
                    {
                        var property = new ComponentProperty();
                        state.Push(property);
                        var component = (Component)parent;
                        component.properties.Add(property);
                        while (reader.MoveToNextAttribute())
                        {
                            if (reader.Name == "name")
                            {
                                property.name = reader.Value;
                            }
                            else if (reader.Name == "type")
                            {
                                property.type = reader.Value;
                            }
                        }
                    }
                    break;
                default:
                    if (reader.NamespaceURI == "component")
                    {
                        var placeHolder = new PassPlaceHolder();
                        state.Push(placeHolder);

                        LinkToParent(placeHolder, parent);

                        placeHolder.placeHolderType = reader.LocalName;

                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(placeHolder, reader);
                        }
                    }
                    else if (onlyLink.TryGetValue(reader.Name, out var createPassLike))
                    {
                        var pass = createPassLike();
                        state.Push(pass);
                        LinkToParent(pass, parent);
                        while (reader.MoveToNextAttribute())
                        {
                            BindAttribute(pass, reader);
                        }
                    }
                    else
                    {
                        state.Push(reader.Name);
                    }
                    break;
            }
            //while (reader.MoveToNextAttribute())
            //{
            //    Console.WriteLine("    {0},{1},{2},{3},{4}", reader.NamespaceURI, reader.LocalName, reader.Depth, reader.NodeType, reader.Value);
            //}

        }

        void ReadText(XmlReader reader)
        {
            var current = state.Peek();
            switch (current)
            {
                case HlslProgram program:
                    program.code = reader.Value;
                    break;
                case Render render:
                    render.x_children.Add(new Script() { source = reader.Value });
                    break;
                case Script script:
                    script.x_children.Add(new Script() { source = reader.Value });
                    break;
                case ShaderVariant variant:
                    variant.keyword = reader.Value;
                    break;
                case ComponentUsing componentUsing:
                    componentUsing.value = reader.Value;
                    break;
            }
        }

        void EndElement()
        {
            var pop = state.Pop();
            switch (pop)
            {
                case HlslProgram hlslProgrm:
                    break;
                case HlslVar hlslvar:
                    break;
                case Pass pass:
                    break;
                case PassPlaceHolder passPlaceHolder:
                    break;
                case Component component:
                    break;
            }
        }

        void LinkToParent(PassLike passLike, object parent)
        {
            switch (parent)
            {
                case Component component:
                    component.children.Add(passLike);
                    break;
                //case Pass pass:
                //    pass.children.Add(passLike);
                //    break;
                //case PassPlaceHolder passPlaceHolder:
                //    passPlaceHolder.children.Add(passLike);
                //    break;
                //case Render render:
                //    render.children.Add(passLike);
                //    break;
                case PassLike passLike1:
                    passLike1.x_children.Add(passLike);
                    break;

            }
        }

        void BindAttribute(PassObject passObject, XmlReader reader)
        {
            var localName = reader.LocalName;
            if (reader.NamespaceURI == "binding")
            {
                passObject.bindings.Add(localName, reader.Value);
                var property = passObject.GetType().GetProperty(localName);
                if (property != null)
                {
                    property.SetValue(passObject, reader.Value);
                }
                var field = passObject.GetType().GetField(localName);
                if (field != null)
                {
                    field.SetValue(passObject, reader.Value);
                }
            }
            else if (reader.NamespaceURI == "directive")
            {
                passObject.directives.Add(localName, reader.Value);
            }
            else if (reader.NamespaceURI == "attribute")
            {
                passObject.x_attributes.Add(localName, reader.Value);
            }
            else if (string.IsNullOrEmpty(reader.NamespaceURI))
            {
                BindAttribute1(passObject, reader);
            }
            //Console.WriteLine("    {0},{1},{2},{3},{4}", reader.NamespaceURI, reader.LocalName, reader.Depth, reader.NodeType, reader.Value);
        }
        void BindAttribute1(object obj, XmlReader reader)
        {
            var localName = reader.LocalName;
            var member = obj.GetType().GetMember(localName);
            if (member.Length > 0)
            {
                switch (member[0])
                {
                    case PropertyInfo propertyInfo:
                        if (propertyInfo.PropertyType == typeof(string))
                        {
                            if (propertyInfo.GetCustomAttribute<QuotesAttribute>() != null)
                                propertyInfo.SetValue(obj, $"\"{reader.Value}\"");
                            else
                                propertyInfo.SetValue(obj, reader.Value);
                        }
                        else if (propertyInfo.PropertyType == typeof(bool))
                        {
                            propertyInfo.SetValue(obj, bool.Parse(reader.Value));
                        }
                        break;
                    case FieldInfo fieldInfo:
                        if (fieldInfo.FieldType == typeof(string))
                        {
                            if (fieldInfo.GetCustomAttribute<QuotesAttribute>() != null)
                                fieldInfo.SetValue(obj, $"\"{reader.Value}\"");
                            else
                                fieldInfo.SetValue(obj, reader.Value);
                        }
                        else if (fieldInfo.FieldType == typeof(bool))
                        {
                            fieldInfo.SetValue(obj, bool.Parse(reader.Value));
                        }
                        break;
                }
            }
        }
    }
}
