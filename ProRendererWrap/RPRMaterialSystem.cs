using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Numerics;

namespace ProRendererWrap
{
    public class RPRMaterialSystem : IDisposable
    {
        public RPRMaterialSystem(RPRContext context, uint type = 0)
        {
            this.Context = context;
            Check(Rpr.ContextCreateMaterialSystem(context._handle, type, out _handle));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
