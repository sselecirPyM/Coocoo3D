using System;
using System.Collections.Generic;

namespace RenderPipelines.LambdaPipe
{
    public delegate void PipelineCall<T>(T config, PipelineContext context);
    public class PipelineBuilder
    {
        public class LambdaRenderer
        {
            public Type ConfigType;
            public object ConfigCall;
            public object RenderCall;
        }

        public Dictionary<Type, LambdaRenderer> renderers = new Dictionary<Type, LambdaRenderer>();
        public Dictionary<Type, IPipelineResourceProvider> pipelineResourceProviders = new Dictionary<Type, IPipelineResourceProvider>();

        public void AddRenderer<T>(PipelineCall<T> config, PipelineCall<T> render) where T : class
        {
            LambdaRenderer lambdaRenderer = new LambdaRenderer();
            lambdaRenderer.ConfigCall = config;
            lambdaRenderer.RenderCall = render;
            lambdaRenderer.ConfigType = typeof(T);
            renderers.Add(typeof(T), lambdaRenderer);
        }

        public void AddPipelineResourceProvider<T>(T provider) where T : IPipelineResourceProvider
        {
            pipelineResourceProviders[typeof(T)] = provider;
        }
    }
}
