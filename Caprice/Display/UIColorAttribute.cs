using System;

namespace Caprice.Display;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class UIColorAttribute : UIShowAttribute
{
    public UIColorAttribute(UIShowType type = UIShowType.Global, string name = null) : base(type, name)
    {

    }
}
