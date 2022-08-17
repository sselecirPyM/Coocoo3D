using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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

    public class RecordSystem : IDisposable
    {
        public Process ffmpegProcess;

        public NamedPipeServerStream pipe;
        public string pipeName;

        public WindowSystem windowSystem;

        public bool ffmpegInstalled;

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
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.UseShellExecute = false;
                processStartInfo.FileName = "ffmpeg";
                processStartInfo.CreateNoWindow = true;
                var process = Process.Start(processStartInfo);
                ffmpegInstalled = true;
                process.CloseMainWindow();
                process.Dispose();
            }
            catch
            {
            }
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
                    var aov = visualChannel1.GetAOV(Caprice.Attributes.AOVType.Color);
                    string fileName;
                    if (pipe == null)
                        fileName = Path.GetFullPath(string.Format("{0}.png", RecordCount));
                    else
                        fileName = Path.GetFullPath(string.Format("{0}.bmp", RecordCount));
                    recorder.Record(aov, fileName);
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
            visualchannel.resolusionSizeSource = ResolusionSizeSource.Custom;

            StartTime = recordSettings.StartTime;
            StopTime = recordSettings.StopTime;
            RecordCount = 0;
            FrameIntervalF = 1 / MathF.Max(recordSettings.FPS, 1e-3f);
            recording = true;

            if (ffmpegInstalled)
            {
                pipeName = Path.GetRandomFileName();
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 1024 * 1024 * 64);
                Task.Run(() =>
                {
                    string[] args =
                    {
                        "-y",
                        "-r",
                        recordSettings.FPS.ToString(),
                        "-i",
                        @"\\.\pipe\" + pipeName,
                        "-c:v",
                        "libx264",
                        "-s",
                        recordSettings.Width + "X" + recordSettings.Height,
                        "-pix_fmt",
                        "yuv420p",
                        "-crf",
                        "16",
                        saveDirectory + @"\output.mp4",
                    };
                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "ffmpeg";
                    foreach (var arg in args)
                        processStartInfo.ArgumentList.Add(arg);
                    ffmpegProcess = Process.Start(processStartInfo);
                });
                recorder.stream = pipe;
                pipe.WaitForConnection();
            }
            else
            {
                recorder.stream = null;
            }
        }

        public void StopRecord()
        {
            recording = false;
            if (pipe != null)
            {
                pipe = null;
            }
            if (windowSystem.visualChannels.TryGetValue(recordChannel, out var visualchannel))
            {
                visualchannel.resolusionSizeSource = ResolusionSizeSource.Default;
            }
        }



        public void Dispose()
        {
            recorder.Dispose();
        }
    }
}
