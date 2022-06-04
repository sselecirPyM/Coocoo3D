using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class GameDriver
    {
        public bool Next(RenderPipelineContext rpContext)
        {
            ref var context = ref rpContext.gameDriverContext;
            ref RecordSettings recordSettings = ref rpContext.recordSettings;

            if (toRecordMode.SetFalse())
            {
                rpContext.recording = true;
                context.Playing = true;
                context.PlaySpeed = 1.0f;
                context.PlayTime = 0.0f;
                context.RequireResetPhysics = true;
                var visualchannel = rpContext.visualChannels[recordChannel];
                visualchannel.outputSize = (recordSettings.Width, recordSettings.Height);
                visualchannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;
                StartTime = recordSettings.StartTime;
                StopTime = recordSettings.StopTime;
                RenderCount = 0;
                RecordCount = 0;
                FrameIntervalF = 1 / MathF.Max(recordSettings.FPS, 1e-3f);
            }
            if (toPlayMode.SetFalse())
            {
                rpContext.recording = false;
            }

            if (rpContext.recording)
                return Recording(rpContext);
            else
                return Playing(rpContext);
        }

        bool Playing(RenderPipelineContext rpContext)
        {
            ref var context = ref rpContext.gameDriverContext;

            var timeManager = context.timeManager;
            if (!timeManager.RealTimerCorrect("frame", context.FrameInterval, out double deltaTime))
            {
                return false;
            }
            if (!(context.NeedRender > 0 || context.Playing))
            {
                return false;
            }
            context.NeedRender -= 1;
            foreach (var visualChannel in rpContext.visualChannels.Values)
            {
                visualChannel.outputSize = visualChannel.sceneViewSize;
                visualChannel.camera.AspectRatio = (float)visualChannel.outputSize.Item1 / (float)visualChannel.outputSize.Item2;
            }

            context.DeltaTime = Math.Clamp(deltaTime * context.PlaySpeed, -0.17f, 0.17f);
            if (context.Playing)
                context.PlayTime += context.DeltaTime;
            return true;
        }

        bool Recording(RenderPipelineContext rpContext)
        {
            ref GameDriverContext context = ref rpContext.gameDriverContext;

            context.NeedRender = 1;

            context.DeltaTime = FrameIntervalF;
            context.PlayTime = FrameIntervalF * RenderCount;

            return true;
        }

        public void AfterRender(RenderPipelineContext rpContext)
        {
            if (!rpContext.recording) return;
            ref GameDriverContext context = ref rpContext.gameDriverContext;
            var visualChannel1 = rpContext.visualChannels[recordChannel];

            if (context.PlayTime >= StartTime && context.PlayTime <= StopTime)
            {
                rpContext.recording = true;
                rpContext.recorder.Record(visualChannel1.GetAOV(Caprice.Attributes.AOVType.Color), Path.GetFullPath(string.Format("{0}.png", RecordCount), saveDirectory));
                RecordCount++;
            }
            foreach (var visualChannel in rpContext.visualChannels.Values)
            {
                if (visualChannel == visualChannel1) continue;
                visualChannel.outputSize = visualChannel.sceneViewSize;
                visualChannel.camera.AspectRatio = (float)visualChannel.outputSize.Item1 / (float)visualChannel.outputSize.Item2;
            }
            if (context.PlayTime > StopTime)
                ToPlayMode();
            RenderCount++;
        }


        public float StartTime;
        public float StopTime;

        public float FrameIntervalF = 1 / 60.0f;
        public int RecordCount = 0;
        public int RenderCount = 0;
        bool toRecordMode;
        bool toPlayMode;
        public string saveDirectory;
        public string recordChannel = "main";
        public void ToRecordMode(string saveDirectory)
        {
            toRecordMode = true;
            this.saveDirectory = saveDirectory;
        }
        public void ToPlayMode()
        {
            toPlayMode = true;
        }
    }
}
