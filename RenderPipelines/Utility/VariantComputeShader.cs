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
            _shader = RenderHelper.CreateComputeShader(source, entryPoint, keywords, fileName);
            variants[keywords] = _shader;
            return _shader;
        }

        public VariantComputeShader(string source, string entryPoint, string fileName = null)
        {
            this.source = source;
            this.entryPoint = entryPoint;
            this.fileName = fileName;
        }

        public readonly string source;
        public readonly string entryPoint;
        public string fileName;

        public void Dispose()
        {
            foreach (var shader in variants.Values)
            {
                shader.Dispose();
            }
            variants.Clear();
        }

        Dictionary<T, ComputeShader> variants = new Dictionary<T, ComputeShader>();
    }

    public class VariantComputeShader : IDisposable
    {
        public ComputeShader Get()
        {
            pso ??= RenderHelper.CreateComputeShader(source, entryPoint, fileName);
            return pso;
        }

        public static implicit operator ComputeShader(VariantComputeShader d) => d.Get();

        public VariantComputeShader(string source, string entryPoint, string fileName = null)
        {
            this.source = source;
            this.entryPoint = entryPoint;
            this.fileName = fileName;
        }

        public readonly string source;
        public readonly string entryPoint;
        public string fileName;

        public void Dispose()
        {
            pso?.Dispose();
            pso = null;
        }

        ComputeShader pso;
    }
}
