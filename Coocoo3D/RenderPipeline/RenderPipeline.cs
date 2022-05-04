using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public abstract class RenderPipeline
    {
        public RenderWrap renderWrap;

        public abstract void BeforeRender();
        public abstract void Render();
        public abstract void AfterRender();
    }
}
