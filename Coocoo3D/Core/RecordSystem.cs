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
    public class RecordSettings
    {
        public float FPS;
        public float StartTime;
        public float StopTime;
        public int Width;
        public int Height;
    }

    public class RecordSystem
    {
        public WindowSystem windowSystem;

        public GameDriverContext gameDriverContext;

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext;

        public string recordChannel = "main";

        public bool recording;

        public float StartTime;
        public float StopTime;
        public float FrameIntervalF = 1 / 60.0f;
        public int RecordCount = 0;
        public string saveDirectory;

        public RecordSettings recordSettings = new RecordSettings()
        {
            FPS = 60,
            Width = 1920,
            Height = 1080,
            StartTime = 0,
            StopTime = 9999,
        };

        public Recorder recorder;

        public void Initialize()
        {
            recorder = new Recorder()
            {
                graphicsDevice = graphicsDevice,
                graphicsContext = graphicsContext,
            };
        }

        public void Record()
        {
            if (recording)
            {
                if (!windowSystem.visualChannels.TryGetValue(recordChannel, out var visualChannel1))
                {
                    return;
                }
                if (gameDriverContext.PlayTime >= StartTime && gameDriverContext.PlayTime <= StopTime)
                {
                    recorder.Record(visualChannel1.GetAOV(Caprice.Attributes.AOVType.Color), Path.GetFullPath(string.Format("{0}.png", RecordCount), saveDirectory));
                    RecordCount++;
                }
            }


            recorder.OnFrame();
        }

        public void StartRecord()
        {
            if (!windowSystem.visualChannels.TryGetValue(recordChannel, out var visualchannel))
            {
                return;
            }

            visualchannel.outputSize = (recordSettings.Width, recordSettings.Height);
            visualchannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;

            StartTime = recordSettings.StartTime;
            StopTime = recordSettings.StopTime;
            RecordCount = 0;
            FrameIntervalF = 1 / MathF.Max(recordSettings.FPS, 1e-3f);
            recording = true;
        }

        public void StopRecord()
        {
            recording = false;
        }
    }
}
