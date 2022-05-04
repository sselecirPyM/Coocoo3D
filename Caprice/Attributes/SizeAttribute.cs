using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caprice.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SizeAttribute : Attribute
    {
        public int X { get; } = 1;
        public int Y { get; } = 1;
        public int Z { get; } = 1;
        public int Mips { get; } = 1;
        public string Source { get; }

        public SizeAttribute(int width, int height = 1, int mips = 1)
        {
            X = width;
            Y = height;
            Mips = mips;
        }

        public SizeAttribute(string source)
        {
            this.Source = source;
        }
    }
}
