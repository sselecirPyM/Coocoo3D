using Caprice.Display;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Coocoo3D.UI;

public class UIUsage
{
    public MemberInfo MemberInfo;

    public string Name;

    public string Description;

    public UIShowType UIShowType;

    public UIColorAttribute colorAttribute;

    public UISliderAttribute sliderAttribute;

    public UIDragFloatAttribute dragFloatAttribute;

    public UIDragIntAttribute dragIntAttribute;

    public UITreeAttribute treeAttribute;


    public static Dictionary<Type, List<UIUsage>> UIUsages = new();

    public static List<UIUsage> GetUIUsage(Type type)
    {
        if (UIUsages.TryGetValue(type, out var uiUsage))
        {
            return uiUsage;
        }
        else
        {
            uiUsage = new List<UIUsage>();
            _GetUsage(uiUsage, type);
            UIUsages[type] = uiUsage;
            return uiUsage;
        }
    }

    static void _GetUsage(List<UIUsage> usages, Type type)
    {
        var members = type.GetMembers();
        foreach (var member in members)
        {
            if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
            {
                _Member(member, usages);
            }
        }
    }

    static void _Member(MemberInfo member, List<UIUsage> usages)
    {
        var uiShowAttribute = member.GetCustomAttribute<UIShowAttribute>();
        var uiDescriptionAttribute = member.GetCustomAttribute<UIDescriptionAttribute>();
        var uiTreeAttribute = member.GetCustomAttribute<UITreeAttribute>();
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
                treeAttribute = member.GetCustomAttribute<UITreeAttribute>(),
                MemberInfo = member,
            };
            usages.Add(usage);
            if (uiDescriptionAttribute != null)
            {
                usage.Description = uiDescriptionAttribute.Description;
            }
            usage.Name ??= member.Name;
        }
        //if (uiTreeAttribute != null)
        //{
        //    switch (member)
        //    {
        //        case FieldInfo fieldInfo:
        //            _GetUsage(usages, fieldInfo.FieldType);
        //            break;
        //        case PropertyInfo propertyInfo:
        //            _GetUsage(usages, propertyInfo.PropertyType);
        //            break;
        //    }
        //}
    }
}
