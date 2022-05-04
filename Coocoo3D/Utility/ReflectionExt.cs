using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public static class ReflectionExt
    {
        public static T GetValue<T>(this MemberInfo memberInfo, object obj)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    return (T)fieldInfo.GetValue(obj);
                case PropertyInfo propertyInfo:
                    return (T)propertyInfo.GetValue(obj);
                default:
                    throw new NotImplementedException();
            }
        }
        public static void SetValue(this MemberInfo memberInfo, object obj, object value)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    fieldInfo.SetValue(obj, value);
                    break;
                case PropertyInfo propertyInfo:
                    propertyInfo.SetValue(obj, value);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public static Type GetGetterType(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    return fieldInfo.FieldType;
                case PropertyInfo propertyInfo:
                    return propertyInfo.PropertyType;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
