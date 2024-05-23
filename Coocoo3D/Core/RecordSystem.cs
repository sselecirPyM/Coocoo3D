using Caprice.Display;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Coocoo3D.Core;

public class TextureRecordData
{
    public ulong frame;
    public int buffOffset;
    public int width;
    public int height;
    public string target;

    public Stream stream;
}
public class RecordSettings
{
    [UIDragFloat(1, 0, 10000, name: "FPS")]
    public float FPS;
    [UIDragFloat(1, 0, 10000, name: "开始时间")]
    public float StartTime;
    [UIDragFloat(1, 0, 10000, name: "结束时间")]
    public float StopTime;
    [UIDragInt(32, 32, 16384, name: "宽度")]
    public int Width;
    [UIDragInt(8, 8, 16384, name: "高度")]
    public int Height;

    public RecordSettings Clone()
    {
        return (RecordSettings)MemberwiseClone();
    }
}

public class RecordSystem : IDisposable
{
    public Process ffmpegProcess;

    public Stream pipe;
    public string pipeName;

    public bool ffmpegInstalled;
    public bool useFFmpeg;

    public GameDriverContext gameDriverContext;

    public GraphicsDevice graphicsDevice;
    public GraphicsContext graphicsContext;

    public EditorContext editorContext;

    public float StartTime;
    public int maxRecordCount;
    public int RecordCount = 0;
    public string saveDirectory;

    public RecordSettings recordSettings = new RecordSettings()
    {
        FPS = 60,
        Width = 1920,
        Height = 1080,
        StartTime = 0,
        StopTime = 300,
    };

    bool recording;

    public VisualChannel recordTarget;

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
            useFFmpeg = true;
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
            var visualChannel1 = recordTarget;
            if (visualChannel1 == null || visualChannel1.disposed)
            {
                return;
            }
            if (gameDriverContext.PlayTime >= StartTime && RecordCount < maxRecordCount)
            {
                var aov = visualChannel1.GetAOV(Caprice.Attributes.AOVType.Color);
                string fileName;
                if (pipe == null)
                    fileName = Path.GetFullPath(string.Format("{0}.png", RecordCount), saveDirectory);
                else
                    fileName = Path.GetFullPath(string.Format("{0}.bmp", RecordCount), saveDirectory);

                bool endFrame = RecordCount + 1 >= maxRecordCount;


                Record(aov, pipe, fileName, endFrame);
                RecordCount++;
            }
        }
    }

    void Record(Texture2D texture, Stream stream, string output, bool endFrame)
    {
        graphicsContext.ReadBack(texture, (info, data) =>
        {
            int width = info.width;
            int height = info.height;
            if (stream == null)
            {
                TextureHelper.SaveToFile(data, width, height, output);
            }
            else
            {
                TextureHelper.SaveToFile(data, width, height, output, stream);
                stream.Flush();
            }
            if (endFrame)
            {
                pipe?.Dispose();
                pipe = null;
            }
        }, null);
    }

    public void StartRecord()
    {
        var visualchannel = editorContext.currentChannel;

        if (visualchannel == null)
        {
            return;
        }

        recordTarget = visualchannel;

        visualchannel.outputSize = (recordSettings.Width, recordSettings.Height);
        visualchannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;
        visualchannel.resolusionSizeSource = ResolusionSizeSource.Custom;

        StartTime = recordSettings.StartTime;
        maxRecordCount = (int)((recordSettings.StopTime - recordSettings.StartTime) * recordSettings.FPS);

        RecordCount = 0;
        recording = true;

        if (useFFmpeg)
        {
            StartRecordFFmpeg();
        }
        else
        {
            pipe?.Dispose();
            pipe = null;
        }
    }

    void StartRecordFFmpeg()
    {
        pipeName = Path.GetRandomFileName();
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 1024 * 1024 * 64);
        this.pipe = pipe;
        Task.Run(() =>
        {
            string[] args =
            {
                "-y",
                "-r",
                recordSettings.FPS.ToString(),
                "-colorspace","bt709",
                "-i", @"\\.\pipe\" + pipeName,
                "-c:v", "libx264",
                "-s", recordSettings.Width + "X" + recordSettings.Height,
                "-vf", "format=yuv420p",
                //"-preset", "medium",
                "-crf", "17",
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

    public void StopRecord()
    {
        recording = false;
        if (recordTarget != null)
        {
            recordTarget.resolusionSizeSource = ResolusionSizeSource.Default;
        }
        recordTarget = null;
    }

    public void Update()
    {
        Record();
    }

    public void Dispose()
    {
    }
}
