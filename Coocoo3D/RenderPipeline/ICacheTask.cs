using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public interface ICacheTask
    {
        public string CachePath { get; }
        public void CacheInvalid();
    }
}
