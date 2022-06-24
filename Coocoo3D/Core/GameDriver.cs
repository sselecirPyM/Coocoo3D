using Coocoo3D.Common;
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
    public class GameDriverContext
    {
        public int NeedRender;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public float FrameInterval;
        public float PlaySpeed;
        public bool RequireResetPhysics;
        public TimeManager timeManager;

        public void RequireRender(bool updateEntities)
        {
            if (updateEntities)
                RequireResetPhysics = true;
            NeedRender = 10;
        }
    }

    public class GameDriver
    {
        public GameDriverContext gameDriverContext = new GameDriverContext()
        {
            FrameInterval = 1 / 240.0f,
        };

        public bool Next(WindowSystem windowSystem, RecordSystem recordSystem)
        {
            RenderPipelineContext rpContext = windowSystem.RenderPipelineContext;

            ref RecordSettings recordSettings = ref recordSystem.recordSettings;

            if (toRecordMode.SetFalse())
            {
                rpContext.recording = true;
                gameDriverContext.Playing = true;
                gameDriverContext.PlaySpeed = 1.0f;
                gameDriverContext.PlayTime = 0.0f;
                gameDriverContext.RequireResetPhysics = true;

                StartTime = recordSettings.StartTime;
                StopTime = recordSettings.StopTime;
                RenderCount = 0;
                FrameIntervalF = 1 / MathF.Max(recordSettings.FPS, 1e-3f);

                recordSystem.saveDirectory = saveDirectory;
                recordSystem.StartRecord();
            }
            if (toPlayMode.SetFalse())
            {
                rpContext.recording = false;
                recordSystem.StopRecord();
            }

            if (rpContext.recording)
                return Recording(gameDriverContext);
            else
                return Playing(gameDriverContext);
        }

        bool Playing(GameDriverContext context)
        {
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

            context.DeltaTime = Math.Clamp(deltaTime * context.PlaySpeed, -0.17f, 0.17f);
            if (context.Playing)
                context.PlayTime += context.DeltaTime;
            return true;
        }

        bool Recording(GameDriverContext context)
        {
            context.NeedRender = 1;

            context.DeltaTime = FrameIntervalF;
            context.PlayTime = FrameIntervalF * RenderCount;

            return true;
        }

        public void AfterRender(WindowSystem windowSystem)
        {
            RenderPipelineContext rpContext = windowSystem.RenderPipelineContext;
            if (!rpContext.recording) return;
            ref GameDriverContext context = ref gameDriverContext;
            if (!windowSystem.visualChannels.TryGetValue(recordChannel, out var visualChannel1))
            {
                ToPlayMode();
                return;
            }


            if (context.PlayTime > StopTime)
                ToPlayMode();
            RenderCount++;
        }


        public float StartTime;
        public float StopTime;

        public float FrameIntervalF = 1 / 60.0f;
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
