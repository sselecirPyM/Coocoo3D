using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using DefaultEcs.System;
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
    public class TextureRecordData
    {
        public ulong frame;
        public int offset;
        public int width;
        public int height;
        public string target;

        public Stream stream;
    }
    public class RecordSettings
    {
        public float FPS;
        public float StartTime;
        public float StopTime;
        public int Width;
        public int Height;
    }

    public class RecordSystem : ISystem<State>
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

        public bool IsEnabled { get; set; } = true;

        public void Initialize()
        {
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
                    Record(aov, pipe, fileName);
                    RecordCount++;
                }
            }

            OnFrame();
            if (recordQueue.Count == 0 && !recording && pipe != null)
            {
                pipe.Dispose();
                pipe = null;
            }
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
                pipe.WaitForConnection();
            }
            else
            {
                pipe?.Dispose();
                pipe = null;
            }
        }

        public void StopRecord()
        {
            recording = false;
            if (windowSystem.visualChannels.TryGetValue(recordChannel, out var visualchannel))
            {
                visualchannel.resolusionSizeSource = ResolusionSizeSource.Default;
            }
        }

        public void Update(State state)
        {
            Record();
        }

        public ReadBackBuffer ReadBackBuffer = new ReadBackBuffer();

        byte[] temp;

        public void OnFrame()
        {
            if (recordQueue.Count == 0) return;
            ulong completed = graphicsDevice.GetInternalCompletedFenceValue();
            while (recordQueue.Count > 0 && recordQueue.Peek().frame <= completed)
            {
                var tuple = recordQueue.Dequeue();
                int width = tuple.width;
                int height = tuple.height;
                var stream = tuple.stream;

                if (temp == null || temp.Length != width * height * 4)
                {
                    temp = new byte[width * height * 4];
                }
                var data = temp;
                ReadBackBuffer.GetData<byte>(tuple.offset, height, (width * 4 + 255) & ~255, width * 4, data);

                if (stream == null)
                    TextureHelper.SaveToFile(data, width, height, tuple.target);
                else
                {
                    TextureHelper.SaveToFile(data, width, height, tuple.target, stream);
                    stream.Flush();
                }
            }
        }

        public Queue<TextureRecordData> recordQueue = new();

        public void Record(Texture2D texture, Stream stream, string output)
        {
            int width = texture.width;
            int height = texture.height;
            if (ReadBackBuffer.size < ((width * 4 + 255) & ~255) * height)
            {
                graphicsContext.UpdateReadBackTexture(ReadBackBuffer, width, height, 4);
            }

            int offset = graphicsContext.ReadBack(ReadBackBuffer, texture);

            recordQueue.Enqueue(new TextureRecordData
            {
                frame = graphicsDevice.GetInternalFenceValue(),
                offset = offset,
                target = output,
                width = width,
                height = height,
                stream = stream
            });
        }

        public void Dispose()
        {
            ReadBackBuffer?.Dispose();
            ReadBackBuffer = null;
        }
    }
}
