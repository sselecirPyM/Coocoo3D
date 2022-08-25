using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public interface IHandler<T>
    {
        public bool Add(T task);

        public void Update();

        public List<T> Output { get; }
    }
}
