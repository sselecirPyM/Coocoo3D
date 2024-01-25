using Coocoo3DGraphics;
using System;
using System.Collections.Generic;

namespace RenderPipelines.Utility
{
    public class VariantComputeShader<T> : IDisposable where T : struct, Enum
    {
        public ComputeShader Get(T keywords)
        {
            if (variants.TryGetValue(keywords, out var _shader))
            {
                return _shader;
            }
            _shader = RenderHelper.CreateComputeShader(source, entryPoint, keywords);
            variants[keywords] = _shader;
            return _shader;
        }

        public VariantComputeShader(string source, string entryPoint)
        {
            this.source = source;
            this.entryPoint = entryPoint;
        }

        public readonly string source;
        public readonly string entryPoint;

        public void Dispose()
        {
            foreach(var shader in variants.Values)
            {
                shader.Dispose();
            }
            variants.Clear();
        }

        Dictionary<T, ComputeShader> variants = new Dictionary<T, ComputeShader>();
    }
}
