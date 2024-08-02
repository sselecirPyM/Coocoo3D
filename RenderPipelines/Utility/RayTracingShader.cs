﻿using Coocoo3DGraphics;
using System.Collections.Generic;

namespace RenderPipelines.Utility;

public class RayTracingShader
{
    public string hlslFile;
    public Dictionary<string, RayTracingShaderDescription> rayGenShaders;

    public Dictionary<string, RayTracingShaderDescription> hitGroups;

    public Dictionary<string, RayTracingShaderDescription> missShaders;

    public int CBVs;
    public int SRVs;
    public int UAVs;

    public int localCBVs;
    public int localSRVs;

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
