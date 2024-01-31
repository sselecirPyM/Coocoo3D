using Coocoo3DGraphics;
using System;
using System.Collections.Generic;

namespace RenderPipelines.Utility
{
    public class VariantShader<T> : IDisposable where T : struct, Enum
    {
        public PSO Get(T keywords)
        {
            if (variants.TryGetValue(keywords, out var _shader))
            {
                return _shader;
            }
            _shader = RenderHelper.CreatePipeline(source, vsEntry, gsEntry, psEntry, keywords);
            variants[keywords] = _shader;
            return _shader;
        }

        public VariantShader(string source, string vsEntry = null, string gsEntry = null, string psEntry = null)
        {
            this.source = source;
            this.vsEntry = vsEntry;
            this.gsEntry = gsEntry;
            this.psEntry = psEntry;
        }

        public readonly string source;
        public readonly string vsEntry;
        public readonly string gsEntry;
        public readonly string psEntry;

        public void Dispose()
        {
            foreach (var shader in variants.Values)
            {
                shader.Dispose();
            }
            variants.Clear();
        }

        Dictionary<T, PSO> variants = new Dictionary<T, PSO>();
    }


    public class VariantShader : IDisposable
    {
        public PSO Get()
        {
            pso ??= RenderHelper.CreatePipeline(source, vsEntry, gsEntry, psEntry, fileName);
            return pso;
        }

        public static implicit operator PSO(VariantShader d) => d.Get();

        public VariantShader(string source, string vsEntry, string gsEntry = null, string psEntry = null, string fileName = null)
        {
            this.source = source;
            this.vsEntry = vsEntry;
            this.gsEntry = gsEntry;
            this.psEntry = psEntry;
            this.fileName = fileName;
        }

        public readonly string source;
        public readonly string vsEntry;
        public readonly string gsEntry;
        public readonly string psEntry;
        public string fileName;

        public void Dispose()
        {
            pso?.Dispose();
            pso = null;
        }

        PSO pso;
    }
}
