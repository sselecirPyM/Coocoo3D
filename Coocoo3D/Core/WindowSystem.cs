using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{

    public class WindowSystem
    {
        public Type[] RenderPipelineTypes = new Type[0];

        public Dictionary<string, VisualChannel> visualChannels = new();

        public VisualChannel currentChannel;

        public RenderPipelineContext RenderPipelineContext;

        public string recordChannel = "main";

        Queue<string> delayAddVisualChannel = new();
        Queue<string> delayRemoveVisualChannel = new();

        public void Initialize()
        {
            LoadRenderPipelines(new DirectoryInfo("Samples"));
            currentChannel = AddVisualChannel("main");
        }

        public void Update()
        {
            var visualChannel1 = visualChannels[recordChannel];
            foreach (var visualChannel in visualChannels.Values)
            {
                if (RenderPipelineContext.recording && visualChannel == visualChannel1) continue;
                visualChannel.outputSize = visualChannel.sceneViewSize;
                visualChannel.camera.AspectRatio = (float)visualChannel.outputSize.Item1 / (float)visualChannel.outputSize.Item2;
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

        public void Update2()
        {
            while (delayAddVisualChannel.TryDequeue(out var vcName))
                AddVisualChannel(vcName);
            while (delayRemoveVisualChannel.TryDequeue(out var vcName))
                RemoveVisualChannel(vcName);
        }

        public void LoadRenderPipelines(DirectoryInfo dir)
        {
            RenderPipelineTypes = new Type[0];
            foreach (var file in dir.EnumerateFiles("*.dll"))
            {
                LoadRenderPipelineTypes(file.FullName);
            }
        }

        public void LoadRenderPipelineTypes(string path)
        {
            try
            {
                RenderPipelineTypes = RenderPipelineTypes.Concat(mainCaches.GetTypes(Path.GetFullPath(path), typeof(RenderPipeline.RenderPipeline))).ToArray();
            }
            catch
            {

            }
        }

        public void Dispose()
        {
            foreach(var vc in visualChannels)
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
            visualChannel.DelaySetRenderPipeline(RenderPipelineTypes[0]);

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
