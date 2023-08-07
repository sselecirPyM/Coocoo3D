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
            var members = type.GetMembers();
            foreach (var member in members)
            {
                if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                {
                    _Member(member, uiUsage);
                }
            }
            UIUsages[type] = uiUsage;
            return uiUsage;
        }
    }

    static void _Member(MemberInfo member, List<UIUsage> usages)
    {
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
    }
}
