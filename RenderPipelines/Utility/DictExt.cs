using Coocoo3DGraphics;
using System.Collections.Generic;
using System.Reflection;

namespace RenderPipelines.Utility
{
    internal static class DictExt
    {
        public static T ConvertToObject<T>(Dictionary<string, object> dict) where T : new()
        {
            var a = typeof(T).GetMembers();

            var inst = new T();

            foreach (var member in a)
            {
                if (!(member is FieldInfo || member is PropertyInfo))
                {
                    continue;
                }
                var type = member.GetGetterType();
                if (dict.TryGetValue(member.Name, out var value) && value.GetType().IsAssignableTo(type))
                {
                    member.SetValue(inst, value);
                }
            }

            return inst;
        }

        public static T ConvertToObject<T>(Dictionary<string, object> dict, RenderHelper renderHelper) where T : new()
        {
            var a = typeof(T).GetMembers();

            var inst = new T();

            foreach (var member in a)
            {
                if (!(member is FieldInfo || member is PropertyInfo))
                {
                    continue;
                }
                var type = member.GetGetterType();
                if (dict.TryGetValue(member.Name, out var value) && value.GetType().IsAssignableTo(type))
                {
                    member.SetValue(inst, value);
                }
                else
                {
                    if (type == typeof(Texture2D))
                    {
                        member.SetValue(inst, renderHelper.renderPipelineView.texError);
                    }
                }
            }

            return inst;
        }
    }
}
