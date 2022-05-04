using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public interface IPassDispatcher
    {
        public void FrameBegin(RenderPipelineContext context);
        public void FrameEnd(RenderPipelineContext context);
        public void Dispatch(UnionShaderParam unionShaderParam);
    }
}
