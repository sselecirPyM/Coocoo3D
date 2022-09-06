using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public interface INavigableTask
    {
        public Type Next { get; }
        public void SetCurrentHandleType(Type type);

        public void OnEnterPipeline() { }
        public void OnLeavePipeline() { }
        public void OnError(Exception exception) { }
    }
}
