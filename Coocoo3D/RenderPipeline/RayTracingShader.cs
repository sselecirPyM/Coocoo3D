using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public class RayTracingShader
    {
        public string hlslFile;
        public Dictionary<string, RayTracingShaderDescription> rayGenShaders;

        public Dictionary<string, RayTracingShaderDescription> hitGroups;

        public Dictionary<string, RayTracingShaderDescription> missShaders;

        public List<SlotRes> CBVs;
        public List<SlotRes> SRVs;
        public List<SlotRes> UAVs;

        public List<SlotRes> localCBVs;
        public List<SlotRes> localSRVs;

        public string[] GetExports()
        {
            List<string> exports = new List<string>();
            if (rayGenShaders != null)
                foreach (var pair in rayGenShaders)
                {
                    exports.Add(pair.Key);
                }
            if (missShaders != null)
                foreach (var pair in missShaders)
                {
                    exports.Add(pair.Key);
                }
            if (hitGroups != null)
                foreach (var pair in hitGroups)
                {
                    if (pair.Value.anyHit != null)
                        exports.Add(pair.Value.anyHit);
                    if (pair.Value.closestHit != null)
                        exports.Add(pair.Value.closestHit);
                    if (pair.Value.intersection != null)
                        exports.Add(pair.Value.intersection);
                }
            return exports.ToArray();
        }
    }
}
