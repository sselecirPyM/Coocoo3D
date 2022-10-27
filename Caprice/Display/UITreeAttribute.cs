using System;

namespace Caprice.Display
{
    public class UITreeAttribute : UIShowAttribute
    {
        public UITreeAttribute()
        {

        }

        public UITreeAttribute(string name) : base(UIShowType.Global, name)
        {

        }
    }
}
