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
    public class RPRImage : IDisposable
    {
        public RPRImage(RPRContext context, string path)
        {
            this.Context = context;
            Check(Rpr.ContextCreateImageFromFile(context._handle, path, out _handle));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
