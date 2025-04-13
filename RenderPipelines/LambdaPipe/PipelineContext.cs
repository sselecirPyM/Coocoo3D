using System;
using System.Collections.Generic;

namespace RenderPipelines.LambdaPipe
{
    public class PipelineContext : IDisposable
    {
        public PipelineBuilder PipelineBuilder { get; set; }

        //public class ConfigContext
        //{
        //    public Dictionary<Type, ConfigContext> configTree = new Dictionary<Type, ConfigContext>();
        //    public object config;
        //}

        //public Dictionary<Type, ConfigContext> configTree = new Dictionary<Type, ConfigContext>();

        //public void ConfigRenderer<T>() where T : class, new()
        //{
        //    var currentNode = configTree;
        //    var type = typeof(T);
        //    T val;
        //    if (configTree.TryGetValue(type, out var configContext))
        //    {
        //        val = (T)configContext.config;
        //    }
        //    else
        //    {
        //        val = new T();
        //        configContext = new ConfigContext();
        //        configContext.config = val;
        //        configTree[type] = configContext;
        //    }
        //    configTree = configContext.configTree;

        //    var configCall = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].configCall;
        //    configCall(val, this);
        //    configTree = currentNode;//back
        //}

        //public void Execute<T>() where T : class
        //{
        //    var currentNode = configTree;
        //    var t = configTree[typeof(T)];
        //    configTree = t.configTree;
        //    var call = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].renderCall;
        //    call((T)t.config, this);
        //    configTree = currentNode;//back
        //}

        public Dictionary<Type, object> configs = new Dictionary<Type, object>();
        public Dictionary<string, object> configKeyed = new Dictionary<string, object>();

        public T GetConfig<T>() where T : class, new()
        {
            if (configs.TryGetValue(typeof(T), out var val))
            {
            }
            else
            {
                val = new T();
                configs[typeof(T)] = val;
            }
            return (T)val;
        }

        public T GetConfigKeyed<T>(string key) where T : class, new()
        {
            if (configKeyed.TryGetValue(key, out var val))
            {
            }
            else
            {
                val = new T();
                configKeyed[key] = val;
            }
            return (T)val;
        }

        public T ConfigRenderer<T>() where T : class, new()
        {
            var val = GetConfig<T>();
            var configCall = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].ConfigCall;
            configCall(val, this);
            return val;
        }
        public T ConfigRenderer<T>(Action<T> onConfig) where T : class, new()
        {
            var val = GetConfig<T>();
            onConfig(val);
            var configCall = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].ConfigCall;
            configCall(val, this);
            return val;
        }
        public T Execute<T>() where T : class
        {
            var val = configs[typeof(T)];
            var call = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].RenderCall;
            call((T)val, this);
            return (T)val;
        }
        public object Execute(Type type)
        {
            var val = configs[type];
            var call = (Delegate)PipelineBuilder.renderers[type].RenderCall;
            return call.DynamicInvoke(val, this);
        }

        //keyed
        public T ConfigRendererKeyed<T>(string key, Action<T> onConfig) where T : class, new()
        {
            var val = GetConfigKeyed<T>(key);
            onConfig(val);
            var configCall = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].ConfigCall;
            configCall(val, this);
            return val;
        }
        public T ExecuteKeyed<T>(string key) where T : class
        {
            var val = configKeyed[key];
            var call = (PipelineCall<T>)PipelineBuilder.renderers[typeof(T)].RenderCall;
            call((T)val, this);
            return (T)val;
        }

        public T GetResourceProvider<T>()
        {
            return (T)PipelineBuilder.pipelineResources[typeof(T)];
        }

        public void Dispose()
        {
            foreach (var pair in PipelineBuilder.pipelineResources)
            {
                if (pair.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            PipelineBuilder.pipelineResources.Clear();
            foreach (var pair in PipelineBuilder.renderers)
            {
                if (pair.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            PipelineBuilder.renderers.Clear();
        }
    }
}
