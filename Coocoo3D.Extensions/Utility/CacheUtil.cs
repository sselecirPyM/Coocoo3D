using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Extensions.Utility
{
    public static class CacheUtil
    {
        public static Dictionary<Texture2D,string> MakeReverse(MainCaches caches)
        {
            Dictionary<Texture2D, string> reverse = new Dictionary<Texture2D, string>();
            foreach (var pair in caches.textureCaches)
            {
                reverse[pair.Value] = pair.Key;
            }
            return reverse;
        }
    }
}
