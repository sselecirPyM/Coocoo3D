using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class UnnamedInputLayout : IEquatable<UnnamedInputLayout>
    {
        public InputElementDescription[] inputElementDescriptions;

        public override bool Equals(object obj)
        {
            return Equals(obj as UnnamedInputLayout);
        }

        public bool Equals(UnnamedInputLayout other)
        {
            if (ReferenceEquals(this, other)) return true;
            return other != null &&
                inputElementDescriptions.SequenceEqual(other.inputElementDescriptions); ;
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new HashCode();
            for (int i = 0; i < inputElementDescriptions.Length; i++)
            {
                hashCode.Add(inputElementDescriptions[i]);
            }
            return hashCode.ToHashCode();
        }
    }
}
