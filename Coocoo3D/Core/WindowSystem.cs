using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class WindowSystem : IDisposable
    {
        public Dictionary<string, VisualChannel> visualChannels = new();

        public VisualChannel currentChannel;

        public RenderPipelineContext RenderPipelineContext;

        Queue<string> delayAddVisualChannel = new();
        Queue<string> delayRemoveVisualChannel = new();

        public void Initialize()
        {
            currentChannel = AddVisualChannel("main");
        }

        public void UpdateChannels()
        {
            foreach (var visualChannel1 in visualChannels)
            {
                var visualChannel = visualChannel1.Value;
                if (visualChannel.resolusionSizeSource == ResolusionSizeSource.Custom)
                    continue;
                visualChannel.outputSize = visualChannel.sceneViewSize;
                (float x, float y) = visualChannel.outputSize;
                visualChannel.camera.AspectRatio = x / y;
            }
        }

        public MainCaches mainCaches = new MainCaches();

        public void DelayAddVisualChannel(string name)
        {
            delayAddVisualChannel.Enqueue(name);
        }
        public void DelayRemoveVisualChannel(string name)
        {
            delayRemoveVisualChannel.Enqueue(name);
        }

        public void Update()
        {
            while (delayAddVisualChannel.TryDequeue(out var vcName))
                AddVisualChannel(vcName);
            while (delayRemoveVisualChannel.TryDequeue(out var vcName))
                RemoveVisualChannel(vcName);
        }

        public void Dispose()
        {
            foreach (var vc in visualChannels)
            {
                vc.Value.Dispose();
            }
            visualChannels.Clear();
        }

        VisualChannel AddVisualChannel(string name)
        {
            var visualChannel = new VisualChannel();
            visualChannels[name] = visualChannel;
            visualChannel.Name = name;
            visualChannel.rpc = RenderPipelineContext;

            return visualChannel;
        }

        void RemoveVisualChannel(string name)
        {
            if (visualChannels.Remove(name, out var vc))
            {
                if (vc == currentChannel)
                    currentChannel = visualChannels.FirstOrDefault().Value;
                vc.Dispose();
            }
        }
    }
}
