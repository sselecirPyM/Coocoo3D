using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class ObjectDescription
    {
        public string Name;
        public string Description;

        public ObjectDescription GetClone()
        {
            return new ObjectDescription()
            {
                Name = Name,
                Description = Description
            };
        }
    }
}
